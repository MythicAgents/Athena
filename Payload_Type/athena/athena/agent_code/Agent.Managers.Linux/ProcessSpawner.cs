using Agent.Interfaces;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using Agent.Models;
using Agent.Utilities;

namespace Agent.Utilities
{
    public class ProcessSpawner : ISpawner
    {
        IMessageManager messageManager;
        Dictionary<string, Process> processes = new Dictionary<string, Process>();
        public ProcessSpawner(IMessageManager messageManager)
        {
            this.messageManager = messageManager;
        }
        public async Task<bool> Spawn(SpawnOptions opts)
        {
            string[] parts = Misc.SplitCommandLine(opts.commandline);
            ProcessStartInfo pInfo = new ProcessStartInfo
            {
                FileName = parts[0],
                Arguments = parts.Length > 1 ? string.Join(" ", parts[1..]) : string.Empty,
                RedirectStandardOutput = opts.output,
                UseShellExecute = !opts.output,
            };

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

            if (proc is null)
            {
                return false;
            }

            this.processes.Add(opts.task_id, proc);
            return true;
        }

        public bool TryGetHandle(string task_id, out SafeProcessHandle? handle)
        {
            if (this.processes.TryGetValue(task_id, out Process? proc))
            {
                handle = proc.SafeHandle;
                return true;
            }

            handle = null;
            return false;
        }
    }
}
