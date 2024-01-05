using Agent.Interfaces;
using Agent.Models;
using System.Runtime.InteropServices;
using System.Text;
//using static Agent.PInvoke;

namespace Agent
{
    public class OSX
    {
        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSUTF8StringEncoding();

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSString_alloc();

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSString_initWithUTF8String(IntPtr str, byte[] utf8);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSString_UTF8String(IntPtr str);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSAutoreleasePool_alloc();

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSAutoreleasePool_init(IntPtr pool);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern void NSAutoreleasePool_release(IntPtr pool);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr OSALanguage_languageForName(byte[] name);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr OSAScript_alloc();

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr OSAScript_initWithSourceLanguage(IntPtr script, IntPtr source, IntPtr language);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr OSAScript_executeAndReturnError(IntPtr script, out IntPtr error);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSDictionary_count(IntPtr dictionary);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSDictionary_objectForKey(IntPtr dictionary, IntPtr key);
        //public OSX()
        //{
        //    utfTextType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "public.utf8-plain-text");
        //    nsStringPboardType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "NSStringPboardType");
        //    generalPasteboard = objc_msgSend(nsPasteboard, generalPasteboardRegister);
        //}

        //private string RunJs(string s)
        //{
        //    IntPtr codeString = objc_msgSend(objc_msgSend(nsString, allocRegister), stringWithUTF8String, s);
        //    IntPtr lang = objc_msgSend(objc_msgSend(osaLanguage, allocRegister), languageForName, "JavaScript");
        //    IntPtr script = objc_msgSend(objc_msgSend(osaScript, allocRegister),
        //                    objc_msgSend(initWithSource, codeString),
        //                    objc_msgSend(language,lang));

        //    //IntPtr script = objc_msgSend(objc_msgSend(osaScript, allocRegister), initWithSource, "language:");
        //    IntPtr runError = objc_getClass("NSDictionary");
        //    IntPtr res = objc_msgSend(script, sel_registerName("executeAndReturnError:"), runError);

        //    Dictionary<object, object> dictRes = (Dictionary<object, object>)Marshal.PtrToStructure(res, typeof(Dictionary<object, object>));

        //    if(dictRes.Count > 0)
        //    {

        //    }

        //}
        //static IntPtr nsString = objc_getClass("NSString");
        //static IntPtr osaLanguage = objc_getClass("OSALanguage");
        //static IntPtr osaScript = objc_getClass("OSAScript");
        //static IntPtr nsPasteboard = objc_getClass("NSPasteboard");
        //static IntPtr nsDictionary = objc_getClass("NSDictionary");
        //static IntPtr nsAppleEventDescriptor = objc_getClass("NSAppleEventDescriptor");
        //static IntPtr nsStringPboardType;
        //static IntPtr utfTextType;
        //static IntPtr generalPasteboard;
        //static IntPtr initWithUtf8Register = sel_registerName("initWithUTF8String:");
        //static IntPtr stringWithUTF8String = sel_registerName("stringWithUTF8String:");
        //static IntPtr initWithSource = sel_registerName("initWithSource:");
        //static IntPtr language = sel_registerName("language:");
        //static IntPtr languageForName = sel_registerName("languageForName:");
        //static IntPtr allocRegister = sel_registerName("alloc");
        //static IntPtr stringForTypeRegister = sel_registerName("stringForType:");
        //static IntPtr utf8Register = sel_registerName("UTF8String");
        //static IntPtr generalPasteboardRegister = sel_registerName("generalPasteboard");
        //[DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        //static extern IntPtr objc_getClass(string className);

        //[DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        //static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        //[DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        //static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

        //[DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        //static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

        //[DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        //static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

        //[DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        //static extern IntPtr sel_registerName(string selectorName);
    }
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
            Dictionary<string, string> args = new Dictionary<string, string>();
            IntPtr result = RunJs(args["code"]);

            await messageManager.WriteLine(Marshal.PtrToStringUTF8(result), job.task.id, true);
            //Console.WriteLine(Marshal.PtrToStringUTF8(result));
        }
        private IntPtr RunJs(string code)
        {
            try
            {
                IntPtr pool = OSX.NSAutoreleasePool_alloc();
                pool = OSX.NSAutoreleasePool_init(pool);

                IntPtr codeString = OSX.NSString_alloc();
                codeString = OSX.NSString_initWithUTF8String(codeString, Encoding.UTF8.GetBytes(code));

                IntPtr lang = OSX.OSALanguage_languageForName(Encoding.UTF8.GetBytes("JavaScript"));

                IntPtr script = OSX.OSAScript_alloc();
                script = OSX.OSAScript_initWithSourceLanguage(script, codeString, lang);

                IntPtr error;
                IntPtr res = OSX.OSAScript_executeAndReturnError(script, out error);

                if (OSX.NSDictionary_count(error) > IntPtr.Zero)
                {
                    IntPtr errorMessageKey = OSX.NSString_alloc();
                    errorMessageKey = OSX.NSString_initWithUTF8String(errorMessageKey, Encoding.UTF8.GetBytes("OSAScriptErrorMessageKey"));

                    IntPtr result = OSX.NSDictionary_objectForKey(error, errorMessageKey);
                    return OSX.NSString_UTF8String(result);
                }

                IntPtr fmtString = OSX.NSString_UTF8String(res);
                OSX.NSAutoreleasePool_release(pool);

                return fmtString;
            }
            catch (Exception exception)
            {
                return OSX.NSString_UTF8String(OSX.NSString_initWithUTF8String(OSX.NSString_alloc(),Encoding.UTF8.GetBytes(exception.Message)));
            }
        //    try
        //    {
        //        IntPtr codeString = NSString.stringWithUTF8String(code);
        //        IntPtr lang = OSALanguage.languageForName("JavaScript");
        //        IntPtr script = OSAScript.alloc().initWithSourceLanguage(codeString, lang);

        //        IntPtr runErrorPtr = IntPtr.Zero;
        //        IntPtr res = OSAScript.executeAndReturnError(script, ref runErrorPtr);

        //        if (runErrorPtr != IntPtr.Zero)
        //        {
        //            IntPtr errorMessageKey = NSDictionary.objectForKey(runErrorPtr, NSString.stringWithUTF8String("OSAScriptErrorMessageKey"));
        //            string result = Marshal.PtrToStringAuto(NSString.UTF8String(errorMessageKey));
        //            return result;
        //        }

        //        string output = Marshal.PtrToStringAuto(NSString.UTF8String(res));
        //        return output;
        //    }
        //    catch (Exception exception)
        //    {
        //        return exception.Message;
        //    }
        }
    }
}
