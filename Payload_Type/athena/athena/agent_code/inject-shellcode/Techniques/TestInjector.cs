using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
//todo implement
namespace Agent.Techniques
{
    internal class TestInjector
    {
        private IntPtr _messageHookHandle;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="windowHandle">IntPtr of the main window handle of the process to inject, i.e. Process.MainWindowHandle.</param>
        /// <param name="assemblyFile">The full path to the .NET assembly to load in the remote process.</param>
        /// <param name="typeFullName">Full type name of the public static class to invoke in the remote process.</param>
        /// <param name="methodName">Name of the static method in that class to invoke in the remote process. Must be a static method, which can also receive arguments, such as 'Start:true:42'</param>
        /// <returns></returns>
        public bool Launch(IntPtr windowHandle, string assemblyFile, string typeFullName, string methodName)
        {
            string assemblyClassAndMethod = assemblyFile + "$" + typeFullName + "$" + methodName;
            IntPtr acmLocal = Marshal.StringToHGlobalUni(assemblyClassAndMethod);

            IntPtr hinstDLL = IntPtr.Zero;

            if (NativeMethods.GetModuleHandleEx(NativeMethods.GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, "MessageHookProc", out hinstDLL))
            {
                uint processID = 0;
                uint threadID = NativeMethods.GetWindowThreadProcessId(windowHandle, out processID);

                if (processID != 0)
                {
                    IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_ALL_ACCESS, false, processID);

                    if (hProcess != IntPtr.Zero)
                    {
                        int buffLen = (assemblyClassAndMethod.Length + 1) * sizeof(char);
                        IntPtr acmRemote = NativeMethods.VirtualAllocEx(hProcess, IntPtr.Zero, buffLen, NativeMethods.MEM_COMMIT, NativeMethods.PAGE_READWRITE);

                        if (acmRemote != IntPtr.Zero)
                        {
                            NativeMethods.WriteProcessMemory(hProcess, acmRemote, acmLocal, buffLen, IntPtr.Zero);

                            _messageHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_CALLWNDPROC, MessageHookProc, hinstDLL, threadID);

                            if (_messageHookHandle != IntPtr.Zero)
                            {
                                NativeMethods.SendMessage(windowHandle, NativeMethods.WM_GOBABYGO, acmRemote, IntPtr.Zero);
                                NativeMethods.UnhookWindowsHookEx(_messageHookHandle);
                                return true;
                            }

                            NativeMethods.VirtualFreeEx(hProcess, acmRemote, 0, NativeMethods.MEM_RELEASE);
                        }

                        NativeMethods.CloseHandle(hProcess);
                    }
                }
                else
                {
                    if (windowHandle == IntPtr.Zero)
                        LogMessage("Invalid window handle received", true);
                    else
                        LogMessage("Could not get process from window handle " + windowHandle.ToString(), true);
                }

                NativeMethods.FreeLibrary(hinstDLL);
            }

            return false;
        }

        private void LogMessage(string message, bool isError)
        {
            // Add your logging logic here
            Console.WriteLine((isError ? "Error: " : "Info: ") + message);
        }

        // Define the delegate for the MessageHookProc
        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        private IntPtr MessageHookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            // Your hook procedure logic here
            return NativeMethods.CallNextHookEx(_messageHookHandle, code, wParam, lParam);
        }

        // NativeMethods class contains the P/Invoke declarations
        private static class NativeMethods
        {
            public const uint GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;
            public const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
            public const uint MEM_COMMIT = 0x1000;
            public const uint PAGE_READWRITE = 0x04;
            public const int WM_GOBABYGO = 0x8001;
            public const int WH_CALLWNDPROC = 4;
            public const uint MEM_RELEASE = 0x8000;

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool GetModuleHandleEx(uint dwFlags, string lpModuleName, out IntPtr phModule);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, uint flAllocationType, uint flProtect);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int nSize, IntPtr lpNumberOfBytesWritten);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hmod, uint dwThreadId);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hModule);
        }
    }
}
