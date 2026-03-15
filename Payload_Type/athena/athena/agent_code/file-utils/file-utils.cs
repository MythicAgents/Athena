using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "file-utils";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<fileutils.FileUtilsArgs>(
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
                    "head" => ExecuteHead(args),
                    "touch" => ExecuteTouch(args),
                    "wc" => ExecuteWc(args),
                    "diff" => ExecuteDiff(args),
                    "link" => ExecuteLink(args),
                    "chmod" => ExecuteChmod(args),
                    "chown" => ExecuteChown(args),
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
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private string ExecuteHead(fileutils.FileUtilsArgs args)
        {
            if (!File.Exists(args.path))
                throw new FileNotFoundException(
                    $"File not found: {args.path}");

            var lines = File.ReadLines(args.path).Take(args.lines);
            return string.Join(Environment.NewLine, lines);
        }

        private string ExecuteTouch(fileutils.FileUtilsArgs args)
        {
            if (File.Exists(args.path))
            {
                File.SetLastWriteTimeUtc(args.path, DateTime.UtcNow);
                return $"Updated timestamp: {args.path}";
            }
            File.Create(args.path).Dispose();
            return $"Created: {args.path}";
        }

        private string ExecuteWc(fileutils.FileUtilsArgs args)
        {
            if (!File.Exists(args.path))
                throw new FileNotFoundException(
                    $"File not found: {args.path}");

            int lineCount = 0;
            int wordCount = 0;
            long byteCount = new FileInfo(args.path).Length;

            foreach (var line in File.ReadLines(args.path))
            {
                lineCount++;
                wordCount += line.Split(
                    (char[])null,
                    StringSplitOptions.RemoveEmptyEntries).Length;
            }

            return $"{lineCount} lines, {wordCount} words, {byteCount} bytes\t{args.path}";
        }

        private string ExecuteDiff(fileutils.FileUtilsArgs args)
        {
            if (!File.Exists(args.path))
                throw new FileNotFoundException(
                    $"File not found: {args.path}");
            if (!File.Exists(args.path2))
                throw new FileNotFoundException(
                    $"File not found: {args.path2}");

            var lines1 = File.ReadAllLines(args.path);
            var lines2 = File.ReadAllLines(args.path2);
            var diff = new List<string>();
            int maxLines = Math.Max(lines1.Length, lines2.Length);

            for (int i = 0; i < maxLines; i++)
            {
                string l1 = i < lines1.Length ? lines1[i] : "";
                string l2 = i < lines2.Length ? lines2[i] : "";
                if (l1 != l2)
                {
                    diff.Add($"@@ line {i + 1} @@");
                    if (i < lines1.Length) diff.Add($"- {l1}");
                    if (i < lines2.Length) diff.Add($"+ {l2}");
                }
            }

            return diff.Count > 0
                ? string.Join(Environment.NewLine, diff)
                : "Files are identical.";
        }

        private string ExecuteLink(fileutils.FileUtilsArgs args)
        {
            if (args.link_type == "symbolic")
                File.CreateSymbolicLink(args.path2, args.path);
            else
                throw new NotSupportedException(
                    "Hard links require platform-specific P/Invoke");

            return $"Created {args.link_type} link: {args.path2} -> {args.path}";
        }

        private string ExecuteChmod(fileutils.FileUtilsArgs args)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return "chmod is only supported on Linux/macOS";

            var mode = Convert.ToInt32(args.mode, 8);
            File.SetUnixFileMode(args.path, (UnixFileMode)mode);
            return $"Changed mode of {args.path} to {args.mode}";
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int chown(string path, int owner, int group);

        private string ExecuteChown(fileutils.FileUtilsArgs args)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return "chown is only supported on Linux/macOS";

            int uid = int.Parse(args.owner);
            int gid = int.Parse(args.group);
            int result = chown(args.path, uid, gid);

            if (result != 0)
                throw new Exception(
                    $"chown failed with error code: {Marshal.GetLastWin32Error()}");

            return $"Changed owner of {args.path} to {args.owner}:{args.group}";
        }
    }
}
