using Athena.Commands.Models;
using Renci.SshNet;
using System.Text;
using Athena.Models;
using Athena.Commands;
using Athena.Models.Responses;

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
                        TaskResponseHandler.AddResponse(RunCommand(args));
                        break;
                    case "connect":
                        TaskResponseHandler.AddResponse(Connect(args));
                        break;
                    case "disconnect":
                        TaskResponseHandler.AddResponse(Disconnect(args));
                        break;
                    case "list-sessions":
                        TaskResponseHandler.AddResponse(ListSessions(args));
                        break;
                    case "switch-session":
                        if (!string.IsNullOrEmpty(args["session"]))
                        {
                            currentSession = args["session"];
                            TaskResponseHandler.AddResponse(new ResponseResult
                            {
                                task_id = args["task-id"],
                                process_response = new Dictionary<string, string> { { "message", "0x36" } },
                                completed = true,
                            });
                        }
                        else
                        {
                            TaskResponseHandler.AddResponse(new ResponseResult
                            {
                                task_id = args["task-id"],
                                process_response = new Dictionary<string, string> { { "message", "0x2D" } },
                                completed = true,
                                status = "error"
                            });
                        }
                        break;
                    default:
                        TaskResponseHandler.AddResponse(new ResponseResult
                        {
                            task_id = args["task-id"],
                            process_response = new Dictionary<string, string> { { "message", "0x2E" } },
                            completed = true,
                            status = "error"
                        });
                        break;
                }

            }
            catch (Exception e)
            {
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
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
                        user_output = $"Initiated Session: {sshClient.ConnectionInfo.Username}@{sshClient.ConnectionInfo.Host} - {guid}",
                        completed = true,
                    };
                }
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    process_response = new Dictionary<string, string> { { "message", "0x31" } },
                    completed = true,
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    user_output = e.ToString(),
                    completed = true,
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
                    process_response = new Dictionary<string, string> { { "message", "0x2D" } },
                    completed = true,
                    status = "error"
                };
            }
            if (!sessions[session].IsConnected)
            {
                sessions.Remove(session);
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    process_response = new Dictionary<string, string> { { "message", "0x32" } },
                    completed = true
                };
            }

            sessions[session].Disconnect();

            if (!sessions[session].IsConnected)
            {
                sessions.Remove(session);
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    process_response = new Dictionary<string, string> { { "message", "0x33" } },
                    completed = true,
                };
            }
            else
            {
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    process_response = new Dictionary<string, string> { { "message", "0x34" } },
                    completed = true,
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
                    process_response = new Dictionary<string, string> { { "message", "0x37" } },
                    completed = true,
                    status = "error"
                };
            }

            if (string.IsNullOrEmpty(command))
            {
                return new ResponseResult
                {
                    task_id = args["task-id"],
                    process_response = new Dictionary<string, string> { { "message", "0x38" } },
                    completed = true,
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
                completed = true,
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
                completed = true,
            };
        }
    }
}

  
