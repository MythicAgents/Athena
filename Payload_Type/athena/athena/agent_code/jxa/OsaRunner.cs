using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace jxa
{
    [SupportedOSPlatform("macos")]
    internal static partial class OsaRunner
    {
        private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

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

        static OsaRunner()
        {
            dlopen(
                "/System/Library/Frameworks/OSAKit.framework/OSAKit",
                2);
            NSStringClass = objc_getClass("NSString");
            StringWithUtf8Sel = sel_getUid("stringWithUTF8String:");
        }

        public static string ExecuteJavaScript(string code)
        {
            IntPtr jsLang = Send(
                objc_getClass("OSALanguage"),
                sel_getUid("languageForName:"),
                "JavaScript");

            if (jsLang == IntPtr.Zero)
                return "Error: JavaScript for Automation not available";

            IntPtr nsCode = Send(
                NSStringClass, StringWithUtf8Sel, code);

            IntPtr script = Send(
                objc_getClass("OSAScript"),
                sel_getUid("alloc"));

            script = Send(
                script,
                sel_getUid("initWithSource:language:"),
                nsCode,
                jsLang);

            IntPtr errorDict = IntPtr.Zero;
            IntPtr result = SendOut(
                script,
                sel_getUid("executeAndReturnError:"),
                ref errorDict);

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
