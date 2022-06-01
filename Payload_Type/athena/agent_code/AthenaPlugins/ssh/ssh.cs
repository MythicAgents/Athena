using PluginBase;
using Renci.SshNet;
using System.Text;

namespace Plugin
{
    public static class ssh
    {
        //static SshClient sshClient;
        static Dictionary<string, SshClient> sessions = new Dictionary<string, SshClient>();
        static string currentSession = "";
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                string action = (string)args["action"];

                switch (action.ToLower())
                {
                    case "exec":
                        return RunCommand(args);
                        break;
                    case "connect":
                        return Connect(args);
                        break;
                    case "disconnect":
                        return Disconnect(args);
                        break;
                    case "list":
                        return ListSessions(args);
                        break;
                    case "switch":
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
            SshClient sshClient = new SshClient(connectionInfo);

            try
            {
                sshClient.Connect();

                if (sshClient.IsConnected)
                {
                    string guid = Guid.NewGuid().ToString();
                    sessions.Add(guid, sshClient);
                    return new ResponseResult
                    {
                        task_id = (string)args["task-id"],
                        user_output = $"Successfully initiated session {guid} ({sshClient.ConnectionInfo.Username}@{sshClient.ConnectionInfo.Host})",
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
            if (sessions[session].IsConnected)
            {
                sessions.Remove(session);
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No client to disconnect from, removing from sessions list",
                    completed = "true"
                };
            }

            sessions[session].Disconnect();

            if (!sessions[session].IsConnected)
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
        static ResponseResult RunCommand(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();
            string command = (string)args["command"];

            if (sessions[currentSession] is null || !sessions[currentSession].IsConnected)
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No active connections. Please use connect to log into a host!",
                    completed = "true",
                    status = "error"
                };
            }

            if (string.IsNullOrEmpty(command))
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No command specified",
                    completed = "true",
                    status = "error"
                };
            }

            SshCommand sc = sessions[currentSession].CreateCommand(command);
            sc.Execute();

            if (sc.ExitStatus != 0)
            {
                sb.AppendLine(sc.Result);
                sb.AppendLine(sc.Error);
                sb.AppendLine($"Exited with code: {sc.ExitStatus}");
            }
            else
            {
                sb.AppendLine(sc.Result);
            }

            return new ResponseResult
            {
                user_output = sb.ToString(),
                completed = "true",
                task_id = (string)args["task-id"]
            };
        }
    
        static ResponseResult ListSessions(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();


            foreach(var sshClient in sessions)
            {
                if (sshClient.Value.IsConnected)
                {
                    sb.AppendLine($"Active - {sshClient.Key}: {sshClient.Value.ConnectionInfo.Username}@{sshClient.Value.ConnectionInfo.Host}");
                }
                else
                {
                    sb.AppendLine($"Disconnected - {sshClient.Key}: {sshClient.Value.ConnectionInfo.Username}@{sshClient.Value.ConnectionInfo.Host}");
                }
            }

            return new ResponseResult
            {
                task_id = (string)args["task-id"],
                user_output = sb.ToString(),
                completed = "true",
            };
        }
    }
}

  
