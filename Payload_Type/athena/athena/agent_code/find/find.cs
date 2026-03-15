using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "find";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<find.FindArgs>(
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
                if (!Directory.Exists(args.path))
                {
                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        completed = true,
                        user_output = $"Directory does not exist: {args.path}",
                        task_id = job.task.id,
                        status = "error"
                    });
                    return;
                }

                var results = new List<string>();
                SearchDirectory(
                    args.path, args, results, 0,
                    job.cancellationtokensource?.Token ?? CancellationToken.None);

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = results.Count > 0
                        ? string.Join(Environment.NewLine, results)
                        : "No matching files found.",
                    task_id = job.task.id,
                });
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private void SearchDirectory(
            string dir, find.FindArgs args, List<string> results,
            int currentDepth, CancellationToken token)
        {
            if (currentDepth > args.max_depth || token.IsCancellationRequested)
                return;

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, args.pattern))
                {
                    if (token.IsCancellationRequested) return;
                    if (MatchesFilters(file, args))
                        results.Add(file);
                }

                foreach (var subDir in Directory.EnumerateDirectories(dir))
                {
                    if (token.IsCancellationRequested) return;
                    SearchDirectory(
                        subDir, args, results, currentDepth + 1, token);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
        }

        private bool MatchesFilters(string filePath, find.FindArgs args)
        {
            var info = new FileInfo(filePath);

            if (args.min_size >= 0 && info.Length < args.min_size) return false;
            if (args.max_size >= 0 && info.Length > args.max_size) return false;

            if (!string.IsNullOrEmpty(args.newer_than))
            {
                if (DateTime.TryParse(args.newer_than, out var newerDate)
                    && info.LastWriteTimeUtc < newerDate)
                    return false;
            }

            if (!string.IsNullOrEmpty(args.older_than))
            {
                if (DateTime.TryParse(args.older_than, out var olderDate)
                    && info.LastWriteTimeUtc > olderDate)
                    return false;
            }

            if (!string.IsNullOrEmpty(args.permissions))
            {
                if (!MatchesPermissions(filePath, args.permissions))
                    return false;
            }

            if (args.action == "grep"
                && !string.IsNullOrEmpty(args.content_pattern))
            {
                try
                {
                    var regex = new Regex(args.content_pattern);
                    foreach (var line in File.ReadLines(filePath))
                    {
                        if (regex.IsMatch(line)) return true;
                    }
                    return false;
                }
                catch { return false; }
            }

            return true;
        }

        private bool MatchesPermissions(string filePath, string permissions)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return true;

            try
            {
                var mode = File.GetUnixFileMode(filePath);
                return permissions switch
                {
                    "suid" => mode.HasFlag(UnixFileMode.SetUser),
                    "sgid" => mode.HasFlag(UnixFileMode.SetGroup),
                    "world-writable" => mode.HasFlag(UnixFileMode.OtherWrite),
                    _ => true
                };
            }
            catch { return false; }
        }
    }
}
