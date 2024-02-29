using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Runtime.InteropServices;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "entitlements";
        private IMessageManager messageManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            int pid = int.Parse(args["pid"]);
            await this.messageManager.WriteLine(GetProcessEntitlements(pid), job.task.id, true);
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
