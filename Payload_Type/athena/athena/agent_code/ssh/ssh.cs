using Renci.SshNet;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IInteractivePlugin
    {
        public string Name => "ssh";
        Dictionary<string, ShellStream> sessions = new Dictionary<string, ShellStream>();
        string currentSession = "";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.logger= logger;
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
                this.messageManager.AddResponse(new TaskResponse
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
                        data = Misc.Base64Encode(System.Text.Encoding.ASCII.GetString(e.Data)),
                        task_id = task_id,
                        message_type = InteractiveMessageType.Output
                    });
                };
                stream.ErrorOccurred += (sender, e) =>
                {
                    messageManager.AddResponse(new InteractMessage()
                    {
                        data = Misc.Base64Encode(e.Exception.ToString()),
                        task_id = task_id,
                        message_type = InteractiveMessageType.Error
                    });
                };
                sessions.Add(task_id, stream);

                return;
            }
            this.messageManager.AddResponse(new TaskResponse
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

        public void Interact(InteractMessage message)
        {
            if (!this.sessions.ContainsKey(message.task_id))
            {
                this.messageManager.AddResponse(new InteractMessage()
                {
                    task_id = message.task_id,
                    data = Misc.Base64Encode("Session exited."),
                    message_type = InteractiveMessageType.Exit,
                });
                return;
            }
            try
            {
                switch (message.message_type)
                {
                    case InteractiveMessageType.Input:
                        this.sessions[message.task_id].Write(Misc.Base64Decode(message.data));
                        break;
                    case InteractiveMessageType.Output:
                        break;
                    case InteractiveMessageType.Error:
                        break;
                    case InteractiveMessageType.Exit:
                        this.sessions[message.task_id].Close();
                        this.sessions[message.task_id].Dispose();
                        this.sessions.Remove(message.task_id);
                        break;
                    case InteractiveMessageType.Escape:
                        this.sessions[message.task_id].WriteByte(0x18);
                        break;
                    case InteractiveMessageType.CtrlA:
                        this.sessions[message.task_id].WriteByte(0x01);
                        break;
                    case InteractiveMessageType.CtrlB:
                        this.sessions[message.task_id].WriteByte(0x02);
                        break;
                    case InteractiveMessageType.CtrlC:
                        this.sessions[message.task_id].WriteByte(0x03);
                        break;
                    case InteractiveMessageType.CtrlD:
                        this.sessions[message.task_id].WriteByte(0x04);
                        break;
                    case InteractiveMessageType.CtrlE:
                        this.sessions[message.task_id].WriteByte(0x05);
                        break;
                    case InteractiveMessageType.CtrlF:
                        this.sessions[message.task_id].WriteByte(0x06);
                        break;
                    case InteractiveMessageType.CtrlG:
                        this.sessions[message.task_id].WriteByte(0x07);
                        break;
                    case InteractiveMessageType.Backspace:
                        this.sessions[message.task_id].WriteByte(0x08);
                        break;
                    case InteractiveMessageType.Tab:
                        this.sessions[message.task_id].WriteByte(0x09);
                        break;
                    case InteractiveMessageType.CtrlK:
                        this.sessions[message.task_id].WriteByte(0x0B);
                        break;
                    case InteractiveMessageType.CtrlL:
                        this.sessions[message.task_id].WriteByte(0x0C);
                        break;
                    case InteractiveMessageType.CtrlN:
                        this.sessions[message.task_id].WriteByte(0x0E);
                        break;
                    case InteractiveMessageType.CtrlP:
                        this.sessions[message.task_id].WriteByte(0x10);
                        break;
                    case InteractiveMessageType.CtrlQ:
                        this.sessions[message.task_id].WriteByte(0x11);
                        break;
                    case InteractiveMessageType.CtrlR:
                        this.sessions[message.task_id].WriteByte(0x12);
                        break;
                    case InteractiveMessageType.CtrlS:
                        this.sessions[message.task_id].WriteByte(0x13);
                        break;
                    case InteractiveMessageType.CtrlU:
                        this.sessions[message.task_id].WriteByte(0x15);
                        break;
                    case InteractiveMessageType.CtrlW:
                        this.sessions[message.task_id].WriteByte(0x17);
                        break;
                    case InteractiveMessageType.CtrlY:
                        this.sessions[message.task_id].WriteByte(0x19);
                        break;
                    case InteractiveMessageType.CtrlZ:
                        this.sessions[message.task_id].WriteByte(0x1A);
                        break;
                    default:
                        break;

                }
            }
            catch (Exception e)
            {
            }
        }
    }
}


