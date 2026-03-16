using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace jxa
{
    [SupportedOSPlatform("macos")]
    internal static partial class OsaRunner
    {
        private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
        private const int ExecutionTimeoutMs = 20_000;

        [LibraryImport(
            "/usr/lib/libdl.dylib",
            StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr dlopen(string path, int mode);

        [LibraryImport(
            ObjCLib,
            StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr sel_getUid(string name);

        [LibraryImport(
            ObjCLib,
            StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr objc_getClass(string name);

        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial IntPtr Send(
            IntPtr self, IntPtr sel);

        [LibraryImport(
            ObjCLib,
            EntryPoint = "objc_msgSend",
            StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr Send(
            IntPtr self, IntPtr sel, string arg);

        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial IntPtr Send(
            IntPtr self, IntPtr sel, IntPtr arg);

        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial IntPtr Send(
            IntPtr self, IntPtr sel, IntPtr arg1, IntPtr arg2);

        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial IntPtr SendOut(
            IntPtr self, IntPtr sel, ref IntPtr outArg);

        private static readonly IntPtr NSStringClass;
        private static readonly IntPtr StringWithUtf8Sel;
        private static readonly IntPtr AllocSel;
        private static readonly IntPtr InitSel;
        private static readonly IntPtr DrainSel;

        static OsaRunner()
        {
            dlopen(
                "/System/Library/Frameworks/OSAKit.framework/OSAKit",
                2);
            NSStringClass = objc_getClass("NSString");
            StringWithUtf8Sel = sel_getUid("stringWithUTF8String:");
            AllocSel = sel_getUid("alloc");
            InitSel = sel_getUid("init");
            DrainSel = sel_getUid("drain");
        }

        public static string ExecuteJavaScript(string code)
        {
            string? result = null;
            Exception? error = null;

            var thread = new Thread(() =>
            {
                try
                {
                    result = ExecuteJavaScriptCore(code);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.IsBackground = true;
            thread.Start();

            if (!thread.Join(ExecutionTimeoutMs))
                return "Error: script execution timed out";

            if (error != null)
                return $"Error: {error.Message}";

            return result ?? "No output";
        }

        private static string ExecuteJavaScriptCore(string code)
        {
            IntPtr pool = Send(
                objc_getClass("NSAutoreleasePool"), AllocSel);
            pool = Send(pool, InitSel);
            Console.WriteLine("[OsaRunner] pool created");

            try
            {
                return ExecuteInPool(code);
            }
            finally
            {
                Send(pool, DrainSel);
                Console.WriteLine("[OsaRunner] pool drained");
            }
        }

        private static string ExecuteInPool(string code)
        {
            IntPtr jsLang = Send(
                objc_getClass("OSALanguage"),
                sel_getUid("languageForName:"),
                "JavaScript");
            Console.WriteLine(
                $"[OsaRunner] languageForName: 0x{jsLang:X}");

            if (jsLang == IntPtr.Zero)
                return "Error: JavaScript for Automation not available";

            IntPtr nsCode = Send(
                NSStringClass, StringWithUtf8Sel, code);
            Console.WriteLine(
                $"[OsaRunner] nsCode: 0x{nsCode:X}");

            IntPtr script = Send(
                objc_getClass("OSAScript"), AllocSel);
            Console.WriteLine(
                $"[OsaRunner] script alloc: 0x{script:X}");

            script = Send(
                script,
                sel_getUid("initWithSource:language:"),
                nsCode,
                jsLang);
            Console.WriteLine(
                $"[OsaRunner] script init: 0x{script:X}");

            if (script == IntPtr.Zero)
                return "Error: failed to create OSAScript";

            Console.WriteLine(
                "[OsaRunner] calling executeAndReturnError:");
            IntPtr errorDict = IntPtr.Zero;
            IntPtr result = SendOut(
                script,
                sel_getUid("executeAndReturnError:"),
                ref errorDict);
            Console.WriteLine(
                $"[OsaRunner] execute done result=0x{result:X} " +
                $"error=0x{errorDict:X}");

            string output;
            if (errorDict != IntPtr.Zero)
            {
                IntPtr errKey = Send(
                    NSStringClass,
                    StringWithUtf8Sel,
                    "OSAScriptErrorMessage");
                IntPtr errMsg = Send(
                    errorDict,
                    sel_getUid("objectForKey:"),
                    errKey);
                output = "Error: " + (GetString(errMsg) ?? "Unknown");
            }
            else
            {
                IntPtr strValue = Send(
                    result, sel_getUid("stringValue"));
                output = GetString(strValue) ?? "No output";
            }

            Send(script, sel_getUid("release"));
            return output;
        }

        private static string? GetString(IntPtr nsString)
        {
            if (nsString == IntPtr.Zero)
                return null;
            IntPtr utf8 = Send(
                nsString, sel_getUid("UTF8String"));
            return Marshal.PtrToStringUTF8(utf8);
        }
    }
}
