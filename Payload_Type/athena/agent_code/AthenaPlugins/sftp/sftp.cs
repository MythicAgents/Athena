using PluginBase;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.Text;

namespace Plugin
{
    public class SftpSession
    {
        public SftpClient client { get; set; }
        public string currPath { get; set; }
        public string parentPath { get; set; }
        public SftpSession(SftpClient client)
        {
            this.client = client;
            this.currPath = "/";

        }
    }

    public static class sftp
    {
        //static sftpClient sftpClient;
        static Dictionary<string, SftpSession> sessions = new Dictionary<string, SftpSession>();
        static string currentSession = "";
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                string action = (string)args["action"];

                switch (action.ToLower())
                {
                    case "upload":
                        //return RunCommand(args);
                        break;
                    case "download":
                        //return RunCommand(args);
                        break;
                    case "connect":
                        return Connect(args);
                        break;
                    case "disconnect":
                        return Disconnect(args);
                        break;
                    case "list-sessions":
                        return ListSessions(args);
                        break;
                    case "switch-session":
                        if (!string.IsNullOrEmpty((string)args["session"]))
                        {
                            currentSession = (string)args["session"];
                            return new ResponseResult
                            {
                                task_id = (string)args["task-id"],
                                user_output = $"Switched session to: {currentSession}",
                                completed = "true",
                            };
                        }
                        else
                        {
                            return new ResponseResult
                            {
                                task_id = (string)args["task-id"],
                                user_output = $"No session specified.",
                                completed = "true",
                                status = "error"
                            };
                        }
                        break;
                    case "ls":
                        return ListDirectories(args);
                        break;
                    case "cd":
                        return ChangeDirectory(args);
                        break;

                }
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No valid command specified.",
                    completed = "true",
                    status = "error"
                };

            }
            catch (Exception ex)
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = ex.ToString(),
                    task_id = (string)args["task-id"], //task-id passed in from Athena
                    status = "error"
                };
            }

        }
        static ResponseResult Connect(Dictionary<string, object> args)
        {
            ConnectionInfo connectionInfo;

            if (args.ContainsKey("keypath") && !String.IsNullOrEmpty((string)args["keypath"])) //SSH Key Auth
            {
                string keyPath = (string)args["keypath"];
                PrivateKeyAuthenticationMethod authenticationMethod;

                if (!String.IsNullOrEmpty((string)args["password"]))
                {
                    PrivateKeyFile pk = new PrivateKeyFile(keyPath, (string)args["password"]);
                    authenticationMethod = new PrivateKeyAuthenticationMethod((string)args["username"], new PrivateKeyFile[] { pk });
                    connectionInfo = new ConnectionInfo((string)args["hostname"], (string)args["username"], authenticationMethod);
                }
                else
                {
                    PrivateKeyFile pk = new PrivateKeyFile(keyPath);
                    authenticationMethod = new PrivateKeyAuthenticationMethod((string)args["username"], new PrivateKeyFile[] { pk });
                    connectionInfo = new ConnectionInfo((string)args["hostname"], (string)args["username"], authenticationMethod);
                }
            }
            else //Username & Password Auth
            {
                PasswordAuthenticationMethod authenticationMethod = new PasswordAuthenticationMethod((string)args["username"], (string)args["password"]);
                connectionInfo = new ConnectionInfo((string)args["hostname"], (string)args["username"], authenticationMethod);
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

                    return new ResponseResult
                    {
                        task_id = (string)args["task-id"],
                        user_output = $"Successfully initiated session {sftpClient.ConnectionInfo.Username}@{sftpClient.ConnectionInfo.Host} - {guid}",
                        completed = "true",
                    };
                }
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"Failed to connect to {(string)args["hostname"]}",
                    completed = "true",
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"Failed to connect to {(string)args["hostname"]}{Environment.NewLine}{e.ToString()}",
                    completed = "true",
                };
            }

        }
        static ResponseResult Disconnect(Dictionary<string, object> args)
        {
            string session;
            if (String.IsNullOrEmpty((string)args["session"]))
            {
                session = currentSession;
            }
            else
            {
                session = (string)args["session"];
            }


            if (!sessions.ContainsKey(session))
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"Session {session} doesn't exist.",
                    completed = "true",
                    status = "error"
                };
            }
            if (!sessions[session].client.IsConnected)
            {
                sessions.Remove(session);
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No client to disconnect from, removing from sessions list",
                    completed = "true"
                };
            }

            sessions[session].client.Disconnect();

            if (!sessions[session].client.IsConnected)
            {
                sessions.Remove(session);
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"Disconnected.",
                    completed = "true",
                };
            }
            else
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"Failed to disconnect",
                    completed = "true",
                    status = "error",
                };
            }
        }
        static FileBrowserResponseResult ListDirectories(Dictionary<string, object> args)
        {
            FileBrowserResponseResult fb = new FileBrowserResponseResult();
            List<FileBrowserFile> directoryFiles = new List<FileBrowserFile>();
            string path;
            if (string.IsNullOrEmpty(currentSession))
            {
                return new FileBrowserResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No active sessions.",
                    completed = "true",
                    status = "error"
                };
            }

            if (!args.ContainsKey("path") || !string.IsNullOrEmpty((string)args["path"]))
            {
                path = sessions[currentSession].client.WorkingDirectory;
            }
            else
            {
                path = (string)args["path"];
            }

            var parentDir = sessions[currentSession].client.Get(GetParentPath(path));
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
                    access_time = new DateTimeOffset(file.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                    modify_time = new DateTimeOffset(file.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
                    name = file.Name,
                    size = file.Length,
                };
                directoryFiles.Add(f);
            }

            return new FileBrowserResponseResult
            {
                task_id = (string)args["task-id"],
                completed = "true",
                user_output = "done",
                file_browser = new FileBrowser
                {
                    host = sessions[currentSession].client.ConnectionInfo.Host,
                    is_file = false,
                    success = true,
                    name = path,
                    files = directoryFiles,
                    parent_path = parentDir.FullName,
                    access_time = new DateTimeOffset(parentDir.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                    modify_time = new DateTimeOffset(parentDir.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
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
        static ResponseResult ListSessions(Dictionary<string, object> args)
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
                    sb.AppendLine($"Disconnected - {sftpClient.Key} - {sftpClient.Value.client.ConnectionInfo.Username}@{sftpClient.Value.client.ConnectionInfo.Host}");
                }
            }

            return new ResponseResult
            {
                task_id = (string)args["task-id"],
                user_output = sb.ToString(),
                completed = "true",
            };
        }
        static ResponseResult ChangeDirectory(Dictionary<string, object> args)
        {
            if (string.IsNullOrEmpty(currentSession))
            {
                return new FileBrowserResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No active sessions.",
                    completed = "true",
                    status = "error"
                };
            }

            if (string.IsNullOrEmpty((string)args["path"]))
            {
                return new FileBrowserResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No path specified.",
                    completed = "true",
                    status = "error"
                };
            }
            sessions[currentSession].client.ChangeDirectory((string)args["path"]);
            return new FileBrowserResponseResult
            {
                task_id = (string)args["task-id"],
                user_output = $"Changed directory to {(string)args["path"]}.",
                completed = "true",
            };
        }
        static string GetParentPath(string path)
        {
            string[] pathParts = path.Replace('\\', '/').Split('/');
            if(pathParts.Count() == 2)
            {
                return "/";
            }
            else
            {
                pathParts = pathParts.Take(pathParts.Count() - 1).ToArray();
                return string.Join('/', pathParts);
            }
        }
    }
}


