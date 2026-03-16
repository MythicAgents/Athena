using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace jxa
{
    [SupportedOSPlatform("macos")]
    internal static partial class OsaRunner
    {
        private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
        private const string CFLib =
            "/System/Library/Frameworks/CoreFoundation.framework" +
            "/CoreFoundation";
        private const int ExecutionTimeoutMs = 20_000;

        [LibraryImport(
            "/usr/lib/libdl.dylib",
            StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr dlopen(string path, int mode);

        [LibraryImport(
            "/usr/lib/libdl.dylib",
            StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr dlsym(
            IntPtr handle, string symbol);

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

        [LibraryImport(CFLib)]
        private static partial IntPtr CFRunLoopGetCurrent();

        [LibraryImport(CFLib)]
        private static partial void CFRunLoopStop(IntPtr rl);

        [LibraryImport(CFLib)]
        private static partial int CFRunLoopRunInMode(
            IntPtr mode,
            double seconds,
            [MarshalAs(UnmanagedType.U1)] bool returnOnSourceHandled);

        private delegate void CFRunLoopTimerCallback(
            IntPtr timer, IntPtr info);

        [LibraryImport(CFLib)]
        private static partial IntPtr CFRunLoopTimerCreate(
            IntPtr allocator,
            double fireDate,
            double interval,
            ulong flags,
            long order,
            CFRunLoopTimerCallback callback,
            IntPtr context);

        [LibraryImport(CFLib)]
        private static partial void CFRunLoopAddTimer(
            IntPtr rl, IntPtr timer, IntPtr mode);

        [LibraryImport(CFLib)]
        private static partial double CFAbsoluteTimeGetCurrent();

        private static readonly IntPtr NSStringClass;
        private static readonly IntPtr StringWithUtf8Sel;
        private static readonly IntPtr AllocSel;
        private static readonly IntPtr InitSel;
        private static readonly IntPtr DrainSel;
        private static readonly IntPtr KCFRunLoopDefaultMode;

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

            IntPtr cfHandle = dlopen(CFLib, 2);
            IntPtr symPtr = dlsym(cfHandle, "kCFRunLoopDefaultMode");
            KCFRunLoopDefaultMode = Marshal.ReadIntPtr(symPtr);
        }

        public static string ExecuteJavaScript(string code)
        {
            string? result = null;
            Exception? error = null;

            var thread = new Thread(() =>
            {
                IntPtr pool = Send(
                    objc_getClass("NSAutoreleasePool"), AllocSel);
                pool = Send(pool, InitSel);

                try
                {
                    IntPtr rl = CFRunLoopGetCurrent();

                    CFRunLoopTimerCallback cb = (_, _) =>
                    {
                        try
                        {
                            result = ExecuteInPool(code);
                        }
                        catch (Exception ex)
                        {
                            error = ex;
                        }
                        finally
                        {
                            CFRunLoopStop(rl);
                        }
                    };

                    double fireTime =
                        CFAbsoluteTimeGetCurrent() + 0.001;
                    IntPtr timer = CFRunLoopTimerCreate(
                        IntPtr.Zero,
                        fireTime,
                        0,
                        0,
                        0,
                        cb,
                        IntPtr.Zero);
                    CFRunLoopAddTimer(
                        rl, timer, KCFRunLoopDefaultMode);

                    CFRunLoopRunInMode(
                        KCFRunLoopDefaultMode,
                        ExecutionTimeoutMs / 1000.0,
                        false);
                }
                finally
                {
                    Send(pool, DrainSel);
                }
            });
            thread.IsBackground = true;
            thread.Start();

            if (!thread.Join(ExecutionTimeoutMs + 2000))
                return "Error: script execution timed out";

            if (error != null)
                return $"Error: {error.Message}";

            return result ?? "No output";
        }

        private static string ExecuteInPool(string code)
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
                objc_getClass("OSAScript"), AllocSel);

            script = Send(
                script,
                sel_getUid("initWithSource:language:"),
                nsCode,
                jsLang);

            if (script == IntPtr.Zero)
                return "Error: failed to create OSAScript";

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
