using Workflow.Contracts;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow.Utilities
{
    public class RuntimeExecutor : IRuntimeExecutor
    {
        IDataBroker messageManager;
        Dictionary<string, Process> processes = new Dictionary<string, Process>();
        public RuntimeExecutor(IDataBroker messageManager)
        {
            this.messageManager = messageManager;
        }
        public async Task<bool> Spawn(SpawnOptions opts)
        {
            ProcessStartInfo pInfo = new ProcessStartInfo();

            string[] parts = Misc.SplitCommandLine(opts.commandline);

            string executable_name = parts[0];

            string arguments = string.Empty;

            if(parts.Length > 1)
            {
                arguments = string.Join(" ", parts[1..]);
            }

            pInfo.FileName = executable_name; 
            pInfo.Arguments = arguments;
            
            if(opts.output)
            {
                pInfo.RedirectStandardOutput = true;
                pInfo.UseShellExecute = false;
            }

            Process proc = new Process()
            {
                StartInfo = pInfo,
                EnableRaisingEvents = true
            };

            if (opts.output)
            {
                proc.OutputDataReceived += (sender, args) => { messageManager.WriteLine(args.Data, opts.task_id, false); };
                proc.ErrorDataReceived += (sender, args) => { messageManager.WriteLine(args.Data, opts.task_id, false, "error"); };
                proc.Exited += (sender, args) => { messageManager.WriteLine("Process Exited.", opts.task_id, true); };
            }

            proc.Start();
            proc.BeginOutputReadLine();

            if(proc is null)
            {
                return false;
            }

            this.processes.Add(opts.task_id, proc);
            return true;
        }

        public bool TryGetHandle(string task_id, out SafeProcessHandle? handle)
        {
            if (this.processes.ContainsKey(task_id))
            {
                handle = this.processes[task_id].SafeHandle;
                return true;
            }

            handle = null;
            return false;
        }
    }
}
