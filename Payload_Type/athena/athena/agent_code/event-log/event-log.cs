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
        public string Name => "event-log";
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
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    messageManager.Write(
                        "Event log is only available on Windows",
                        job.task.id, true, "error");
                    return;
                }

                var args = JsonSerializer.Deserialize<event_log.EventLogArgs>(
                    job.task.parameters) ?? new event_log.EventLogArgs();

                string result = args.action switch
                {
                    "query" => QueryLog(args.log_name, args.count),
                    "list" => ListLogs(),
                    "etw-control" => "ETW provider control is not yet implemented",
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

        private string QueryLog(string logName, int count)
        {
            var eventLog = new EventLog(logName);
            var entries = new List<Dictionary<string, object?>>();
            int start = Math.Max(0, eventLog.Entries.Count - count);

            for (int i = eventLog.Entries.Count - 1; i >= start && entries.Count < count; i--)
            {
                try
                {
                    var entry = eventLog.Entries[i];
                    entries.Add(new Dictionary<string, object?>
                    {
                        ["index"] = entry.Index,
                        ["time"] = entry.TimeGenerated.ToString("o"),
                        ["source"] = entry.Source,
                        ["type"] = entry.EntryType.ToString(),
                        ["event_id"] = entry.InstanceId,
                        ["message"] = entry.Message?.Length > 500
                            ? entry.Message[..500] + "..."
                            : entry.Message
                    });
                }
                catch { }
            }
            eventLog.Dispose();

            return JsonSerializer.Serialize(entries,
                new JsonSerializerOptions { WriteIndented = true });
        }

        private string ListLogs()
        {
            EventLog[] logs;
            try
            {
                logs = EventLog.GetEventLogs();
            }
            catch (System.Security.SecurityException ex)
            {
                throw new InvalidOperationException(
                    $"Insufficient privileges to list event logs: {ex.Message}", ex);
            }

            var result = logs.Select(l =>
            {
                int count = 0;
                try { count = l.Entries.Count; } catch { }
                return new { name = l.LogDisplayName, entries = count, source = l.Log };
            }).ToList();

            foreach (var l in logs) l.Dispose();

            return JsonSerializer.Serialize(result,
                new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
