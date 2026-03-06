using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;
using System.Runtime.InteropServices;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "entitlements";
        private IDataBroker messageManager { get; set; }
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            int pid = int.Parse(args["pid"]);
            DebugLog.Log($"{Name} getting entitlements for pid {pid} [{job.task.id}]");
            this.messageManager.WriteLine(GetProcessEntitlements(pid), job.task.id, true);
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
        string GetProcessEntitlements(int pid)
        {
            // Get the process path
            string processPath = GetProcessPath(pid); ;

            return GetEntitlementsFromBundle(processPath);
        }
        string GetProcessPath(int pid)
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                buffer = Marshal.AllocHGlobal(Native.PROC_PIDPATHINFO_MAXSIZE);
                int length = Native.proc_pidpath(pid, buffer, Native.PROC_PIDPATHINFO_MAXSIZE);

                if (length > 0)
                {
                    return Marshal.PtrToStringAnsi(buffer, length);
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        string GetEntitlementsFromBundle(string bundlePath)
        {
            return ReadInfoPlist(bundlePath);
        }
        string ReadInfoPlist(string bundlePath)
        {
            Console.WriteLine(bundlePath);
            string infoPlistPath = Path.Combine(bundlePath, "../", "../", "../", "Contents", "Info.plist");

            if (!File.Exists(infoPlistPath))
            {
                return "Info.plist file does not exist.";
            }

            try
            {
                return File.ReadAllText(infoPlistPath);
            }
            catch (Exception ex)
            {
                return $"Error reading Info.plist: {ex.Message}";
            }
        }
    }
}
