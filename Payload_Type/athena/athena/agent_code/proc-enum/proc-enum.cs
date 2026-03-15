using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "proc-enum";
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
                var args = JsonSerializer.Deserialize<proc_enum.ProcEnumArgs>(
                    job.task.parameters) ?? new proc_enum.ProcEnumArgs();

                string result = args.action switch
                {
                    "proc-enum" => EnumProcesses(),
                    "named-pipes" => EnumNamedPipes(),
                    _ => throw new ArgumentException($"Unknown action: {args.action}")
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id
                });
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private string EnumProcesses()
        {
            var processes = new List<Dictionary<string, object>>();
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var info = new Dictionary<string, object>
                    {
                        ["pid"] = proc.Id,
                        ["name"] = proc.ProcessName,
                        ["threads"] = proc.Threads.Count,
                        ["memory_mb"] = proc.WorkingSet64 / (1024 * 1024)
                    };
                    try { info["start_time"] = proc.StartTime.ToString("o"); }
                    catch { }
                    processes.Add(info);
                }
                catch { }
                finally { proc.Dispose(); }
            }
            return JsonSerializer.Serialize(processes,
                new JsonSerializerOptions { WriteIndented = true });
        }

        private string EnumNamedPipes()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException(
                    "Named pipes enumeration is only available on Windows");

            var pipes = Directory.GetFiles(@"\\.\pipe\");
            return JsonSerializer.Serialize(pipes,
                new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
