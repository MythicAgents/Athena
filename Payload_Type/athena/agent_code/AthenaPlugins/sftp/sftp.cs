using PluginBase;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;

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
        public static void Execute(Dictionary<string, object> args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                string action = (string)args["action"];

                switch (action.ToLower())
                {
                    case "upload":
                        //return RunCommand(args);
                        PluginHandler.AddResponse(new ResponseResult
                        {
                            task_id = (string)args["task-id"],
                            user_output = "Sorry, this function is not yet supported",
                            completed = "true",
                            status = "error"
                        });
                        break;
                    case "download":
                        PluginHandler.AddResponse(DownloadFile(args));
                        break;
                    case "connect":
                        PluginHandler.AddResponse(Connect(args));
                        break;
                    case "disconnect":
                        PluginHandler.AddResponse(Disconnect(args));
                        break;
                    case "list-sessions":
                        PluginHandler.AddResponse(ListSessions(args));
                        break;
                    case "switch-session":
                        if (!string.IsNullOrEmpty((string)args["session"]))
                        {
                            currentSession = (string)args["session"];
                            PluginHandler.AddResponse(new ResponseResult
                            {
                                task_id = (string)args["task-id"],
                                user_output = $"Switched session to: {currentSession}",
                                completed = "true",
                            });
                        }
                        else
                        {
                            PluginHandler.AddResponse(new ResponseResult
                            {
                                task_id = (string)args["task-id"],
                                user_output = $"No session specified.",
                                completed = "true",
                                status = "error"
                            });
                        }
                        break;
                    case "ls":
                        PluginHandler.AddResponse(ListDirectories(args));
                        break;
                    case "cd":
                        PluginHandler.AddResponse(ChangeDirectory(args));
                        break;
                    case "pwd":
                        PluginHandler.AddResponse(GetCurrentDirectory(args));
                        break;

                }
                PluginHandler.AddResponse(new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No valid command specified.",
                    completed = "true",
                    status = "error"
                });
            }
            catch (Exception e)
            {
                PluginHandler.WriteOutput(e.ToString(), (string)args["task-id"], true, "error");
                return;
            }
        }

        static ResponseResult DownloadFile(Dictionary<string, object> args)
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
            else if(!args.ContainsKey("path") || string.IsNullOrEmpty((string)args["path"]))
            {
                return new FileBrowserResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No file specified.",
                    completed = "true",
                    status = "error"
                };
            }
            string output = "";

            try
            {
                using (var remoteFileStream = sessions[currentSession].client.OpenRead(((string)args["path"]).Replace("\"", "")))
                {
                    var textReader = new System.IO.StreamReader(remoteFileStream);
                    output = textReader.ReadToEnd();
                }

                if (!string.IsNullOrEmpty(output))
                {
                    return new FileBrowserResponseResult
                    {
                        task_id = (string)args["task-id"],
                        user_output = output,
                        completed = "true",
 
                    };
                }
                else
                {
                    return new FileBrowserResponseResult
                    {
                        task_id = (string)args["task-id"],
                        user_output = $"File stream was empty.",
                        completed = "true",
                        status = "error"
                    };
                }
            }
            catch (Exception e) {
                return new FileBrowserResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = e.ToString(),
                    completed = "true",
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
            if (!args.ContainsKey("path") || string.IsNullOrEmpty((string)args["path"]))
            {
                path = sessions[currentSession].client.WorkingDirectory;
            }
            else if (((string)args["path"]).StartsWith('/'))
            {
                path = (string)args["path"];
            }
            else if (((string)args["path"]) == ".")
            {
                path = sessions[currentSession].client.WorkingDirectory;
            }
            else if (((string)args["path"]).Contains("../"))
            {
                string curPath = NormalizePath(sessions[currentSession].client.WorkingDirectory);
                var numdirs = Regex.Matches((string)args["path"], @"(\.\.\/)").Count;
                for (int i = 0; i < numdirs; i++)
                {
                    curPath = GetParentPath(curPath);
                }
                path = "/" + NormalizePath(curPath) + NormalizePath(((string)args["path"]).Replace("../", ""));
            }
            else
            {
                path = sessions[currentSession].client.WorkingDirectory + "/" + ((string)args["path"]);
            }
            
            path = NormalizePath(path);
            SftpFile parentDir = sessions[currentSession].client.Get(NormalizeFullPath(GetParentPath(path)));
                    
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
                    name = Path.GetFileName(path.TrimEnd('/')),
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
                    sessions.Remove(sftpClient.Key);
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
                user_output = $"Changed directory to {sessions[currentSession].client.WorkingDirectory}.",
                completed = "true",
            };
        }
        static ResponseResult GetCurrentDirectory(Dictionary<string, object> args)
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

            return new FileBrowserResponseResult
            {
                task_id = (string)args["task-id"],
                user_output = sessions[currentSession].client.WorkingDirectory,
                completed = "true",
            };
        }
        static string GetParentPath(string path)
        {
            string[] pathParts = path.Replace('\\', '/').Split('/').Where(x=> !string.IsNullOrEmpty(x)).ToArray();
            if(pathParts.Count() <= 1)
            {
                return "/";
            }
            else
            {
                pathParts = pathParts.Take(pathParts.Count() - 1).ToArray();
                return string.Join('/', pathParts);
            }
        }
        static string NormalizePath(string path)
        {
            string normalizedPath = path;
            if (!path.EndsWith('/'))
            {
                normalizedPath = path + '/';
            }
            return normalizedPath;
        }
        static string NormalizeFullPath(string path)
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
    }
}


