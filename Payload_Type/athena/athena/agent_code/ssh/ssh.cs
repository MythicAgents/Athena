using Renci.SshNet;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;


namespace Agent
{
    internal static class SshAuthentication
    {
        internal static bool HasRequiredArguments(SshArgs args) =>
            !string.IsNullOrWhiteSpace(args.hostname) &&
            !string.IsNullOrWhiteSpace(args.username) &&
            (!string.IsNullOrWhiteSpace(args.password) || !string.IsNullOrWhiteSpace(args.keypath));
    }

    public class Plugin : IInteractivePlugin
    {
        public string Name => "ssh";
        readonly SshSessionRegistry<ShellStream> sessions = new();

        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.logger= logger;
        }
        public async Task Execute(ServerJob job)
        {
            //Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            SshArgs args = JsonSerializer.Deserialize<SshArgs>(job.task.parameters);
            if (args is null || !SshAuthentication.HasRequiredArguments(args)) {
                return;
            }

            await this.Connect(args, job.task.id, job.cancellationtokensource.Token);
        }
        private async Task Connect(SshArgs args, string task_id, CancellationToken ct)
        {
            SshEndpoint endpoint = SshEndpoint.Parse(args.hostname);

            ConnectionInfo ci = null;
            if (!string.IsNullOrEmpty(args.keypath))
            {
                ci = this.ConnectWithKey(args, endpoint);
            }
            else
            {
                ci = this.ConnectWithUsernamePass(args, endpoint);
            }

            SshClient sshClient = new SshClient(ci);
            sshClient.HostKeyReceived += (sender, e) =>
            {
                e.CanTrust = SshHostKeyPolicy.IsTrusted(
                    args.host_key_fingerprint,
                    e.FingerPrintSHA256,
                    e.FingerPrintMD5);
            };

            try
            {
                await sshClient.ConnectAsync(ct);
            }
            catch (Exception e)
            {
                this.messageManager.AddTaskResponse(new TaskResponse
                {
                    task_id = task_id,
                    user_output = e.ToString(),
                    completed = true,
                });
                sshClient.Dispose();
                return;
            }

            if (sshClient.IsConnected)
            {
                bool admitted = SshSessionAdmission.TryCreate(
                    sessions,
                    task_id,
                    sshClient,
                    () => sshClient.CreateShellStream("", 80, 30, 0, 0, 0),
                    stream =>
                    {
                        stream.DataReceived += (sender, e) =>
                        {
                            messageManager.AddInteractMessage(new InteractMessage()
                            {
                                data = Misc.Base64Encode(System.Text.Encoding.ASCII.GetString(e.Data)),
                                task_id = task_id,
                                message_type = InteractiveMessageType.Output
                            });
                        };
                        stream.ErrorOccurred += (sender, e) =>
                        {
                            messageManager.AddInteractMessage(new InteractMessage()
                            {
                                data = Misc.Base64Encode(e.Exception.ToString()),
                                task_id = task_id,
                                message_type = InteractiveMessageType.Error
                            });
                        };
                    },
                    out Exception? admissionError);
                if (!admitted)
                {
                    if (admissionError is not null)
                    {
                        messageManager.AddTaskResponse(new TaskResponse
                        {
                            task_id = task_id,
                            user_output = admissionError.ToString(),
                            completed = true,
                        });
                    }
                    return;
                }

                try
                {
                    ct.Register(() => Disconnect(task_id));
                }
                catch (Exception e)
                {
                    sessions.Retire(task_id);
                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        task_id = task_id,
                        user_output = e.ToString(),
                        completed = true,
                    });
                }

                return;
            }
            sshClient.Dispose();
            this.messageManager.AddTaskResponse(new TaskResponse
            {
                task_id = task_id,
                user_output = "Failed to connect to host.",
                completed = true,
            });

        }

        private ConnectionInfo ConnectWithKey(SshArgs args, SshEndpoint endpoint)
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
            return new ConnectionInfo(endpoint.Host, endpoint.Port, args.username, am);
        }
        private ConnectionInfo ConnectWithUsernamePass(SshArgs args, SshEndpoint endpoint)
        {
            PasswordAuthenticationMethod authenticationMethod = new PasswordAuthenticationMethod(args.username, args.password);
            return new ConnectionInfo(endpoint.Host, endpoint.Port, args.username, authenticationMethod);
        }

        public void Interact(InteractMessage message)
        {
            if (!sessions.TryAcquire(message.task_id, out SshSessionLease<ShellStream> lease))
            {
                this.messageManager.AddInteractMessage(new InteractMessage()
                {
                    task_id = message.task_id,
                    data = Misc.Base64Encode("Session exited."),
                    message_type = InteractiveMessageType.Exit,
                });
                return;
            }
            using (lease)
            try
            {
                ShellStream stream = lease.Stream;
                switch (message.message_type)
                {
                    case InteractiveMessageType.Input:
                        stream.Write(Misc.Base64Decode(message.data));
                        break;
                    case InteractiveMessageType.Output:
                        break;
                    case InteractiveMessageType.Error:
                        break;
                    case InteractiveMessageType.Exit:
                        Disconnect(message.task_id);
                        break;
                    case InteractiveMessageType.Escape:
                        stream.WriteByte(0x18);
                        break;
                    case InteractiveMessageType.CtrlA:
                        stream.WriteByte(0x01);
                        break;
                    case InteractiveMessageType.CtrlB:
                        stream.WriteByte(0x02);
                        break;
                    case InteractiveMessageType.CtrlC:
                        stream.WriteByte(0x03);
                        break;
                    case InteractiveMessageType.CtrlD:
                        stream.WriteByte(0x04);
                        break;
                    case InteractiveMessageType.CtrlE:
                        stream.WriteByte(0x05);
                        break;
                    case InteractiveMessageType.CtrlF:
                        stream.WriteByte(0x06);
                        break;
                    case InteractiveMessageType.CtrlG:
                        stream.WriteByte(0x07);
                        break;
                    case InteractiveMessageType.Backspace:
                        stream.WriteByte(0x08);
                        break;
                    case InteractiveMessageType.Tab:
                        stream.WriteByte(0x09);
                        break;
                    case InteractiveMessageType.CtrlK:
                        stream.WriteByte(0x0B);
                        break;
                    case InteractiveMessageType.CtrlL:
                        stream.WriteByte(0x0C);
                        break;
                    case InteractiveMessageType.CtrlN:
                        stream.WriteByte(0x0E);
                        break;
                    case InteractiveMessageType.CtrlP:
                        stream.WriteByte(0x10);
                        break;
                    case InteractiveMessageType.CtrlQ:
                        stream.WriteByte(0x11);
                        break;
                    case InteractiveMessageType.CtrlR:
                        stream.WriteByte(0x12);
                        break;
                    case InteractiveMessageType.CtrlS:
                        stream.WriteByte(0x13);
                        break;
                    case InteractiveMessageType.CtrlU:
                        stream.WriteByte(0x15);
                        break;
                    case InteractiveMessageType.CtrlW:
                        stream.WriteByte(0x17);
                        break;
                    case InteractiveMessageType.CtrlY:
                        stream.WriteByte(0x19);
                        break;
                    case InteractiveMessageType.CtrlZ:
                        stream.WriteByte(0x1A);
                        break;
                    default:
                        break;

                }
            }
            catch
            {
            }
        }

        private void Disconnect(string taskId)
        {
            sessions.Retire(taskId);
        }
    }
}


