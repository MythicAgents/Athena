using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;

namespace Agent
{
    public class Plugin : IInteractivePlugin
    {
        public string Name => "shell";
        private readonly ConcurrentDictionary<string, ProcessRunner> runningProcs = new();
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
    }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);

            string shell;
            if (args.ContainsKey("shell") && !string.IsNullOrEmpty(args["shell"]))
            {
                shell = args["shell"];
            }
            else
            {
                shell = GetDefaultShell();
            }


            ProcessRunner runner = new ProcessRunner(
                shell,
                job.task.id,
                messageManager,
                () => runningProcs.TryRemove(job.task.id, out _));

            if (!runningProcs.TryAdd(job.task.id, runner))
            {
                runner.Dispose();
                messageManager.Write("A shell is already running for this task.", job.task.id, true, "error");
                return;
            }

            runner.Start(job.cancellationtokensource.Token);
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
            if (runningProcs.TryGetValue(message.task_id, out ProcessRunner? runner))
            {
                switch (message.message_type)
                {
                    case InteractiveMessageType.Input:
                        runner.Write(Misc.Base64Decode(message.data));
                        break;
                    case InteractiveMessageType.Output:
                        break;
                    case InteractiveMessageType.Error:
                        break;
                    case InteractiveMessageType.Exit:
                        runner.Stop();
                        break;
                    case InteractiveMessageType.Escape:
                        runner.Write(0x18);
                        break;
                    case InteractiveMessageType.CtrlA:
                        runner.Write(0x01);
                        break;
                    case InteractiveMessageType.CtrlB:
                        runner.Write(0x02);
                        break;
                    case InteractiveMessageType.CtrlC:
                        runner.Write(0x03);
                        break;
                    case InteractiveMessageType.CtrlD:
                        runner.Write(0x04);
                        break;
                    case InteractiveMessageType.CtrlE:
                        runner.Write(0x05);
                        break;
                    case InteractiveMessageType.CtrlF:
                        runner.Write(0x06);
                        break;
                    case InteractiveMessageType.CtrlG:
                        runner.Write(0x07);
                        break;
                    case InteractiveMessageType.Backspace:
                        runner.Write(0x08);
                        break;
                    case InteractiveMessageType.Tab:
                        runner.Write(0x09);
                        break;
                    case InteractiveMessageType.CtrlK:
                        runner.Write(0x0B);
                        break;
                    case InteractiveMessageType.CtrlL:
                        runner.Write(0x0C);
                        break;
                    case InteractiveMessageType.CtrlN:
                        runner.Write(0x0E);
                        break;
                    case InteractiveMessageType.CtrlP:
                        runner.Write(0x10);
                        break;
                    case InteractiveMessageType.CtrlQ:
                        runner.Write(0x11);
                        break;
                    case InteractiveMessageType.CtrlR:
                        runner.Write(0x12);
                        break;
                    case InteractiveMessageType.CtrlS:
                        runner.Write(0x13);
                        break;
                    case InteractiveMessageType.CtrlU:
                        runner.Write(0x15);
                        break;
                    case InteractiveMessageType.CtrlW:
                        runner.Write(0x17);
                        break;
                    case InteractiveMessageType.CtrlY:
                        runner.Write(0x19);
                        break;
                    case InteractiveMessageType.CtrlZ:
                        runner.Write(0x1A);
                        break;
                    default:
                        break;

                }
            }
        }
    }
}
