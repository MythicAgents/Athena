using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;
using System.Runtime.InteropServices;
using Medallion.Shell;
using System.Security.Cryptography;

namespace Plugins
{

    public class InteractiveShell : IPlugin
    {
        public string Name => "interactive_shell";

        public bool Interactive => true;

        Dictionary<string, Process> runningProcs = new Dictionary<string, Process>();
        //Dictionary<string, ConsoleAppManager> managers = new Dictionary<string, ConsoleAppManager>();
        //Dictionary<string, GptInteractiveProcess> managers = new Dictionary<string, GptInteractiveProcess>();
        Dictionary<string, Command> managers = new Dictionary<string, Command>();

        public async void Start(Dictionary<string, string> args)
        {
            string shell = String.Empty;
            if (args.ContainsKey("executable"))
            {
                shell = args["executable"];
            }
            else
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    shell = "/bin/bash";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    shell = "/bin/zsh";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                }
            }

            //ConsoleAppManager manager = new ConsoleAppManager(shell, args["task-id"]);
            //GptInteractiveProcess manager = new GptInteractiveProcess(shell, args["task-id"]);
            var command = Command.Run(shell, new string[] { }, Encoding.UTF8, Encoding.UTF8);
            string line = String.Empty;
            this.managers.Add(args["task-id"], command);
            while ((line = await command.StandardOutput.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                Console.WriteLine(line);
            }

            //manager.StdOutReceived += (sender, e) => { if (e is not null) TaskResponseHandler.Write(e, ((ConsoleAppManager)sender).task_id, false); };
            //manager.StdErrReceived += (sender, e) => { if (e is not null) TaskResponseHandler.Write(e, ((ConsoleAppManager)sender).task_id, false); };
            //command.Process.OutputDataReceived += (sender, e) => { if (e is not null) Console.WriteLine(e, ((GptInteractiveProcess)sender).task_id, false); };
            //manager.OutputReceived += (sender, e) => { if (e is not null) Console.WriteLine(e, ((GptInteractiveProcess)sender).task_id, false); };
            //manager.ErrorReceived += (sender, e) => { if (e is not null) Console.WriteLine(e, ((GptInteractiveProcess)sender).task_id, false); };
            //manager.OutputReceived += (sender, e) => { if (e is not null) Console.WriteLine(e, ((GptInteractiveProcess)sender).task_id, false); };
            //manager.ErrorReceived += (sender, e) => { if (e is not null) Console.WriteLine(e, ((GptInteractiveProcess)sender).task_id, false); };
            //manager.StdOutReceived += (sender, e) => { if (e is not null) Console.Write(e, ((ConsoleAppManager)sender).task_id, false); };
            //manager.StdErrReceived += (sender, e) => { if (e is not null) Console.Write(e, ((ConsoleAppManager)sender).task_id, false); };
            //manager.ProcessExited += (sender, e) => { 
            //    if (e is not null) TaskResponseHandler.Write("Process Exited.", ((ConsoleAppManager)sender).task_id, true);
            //    Stop(((ConsoleAppManager)sender).task_id);
            //};
            //manager.ExecuteAsync();
        }

        public void Interact(InteractiveMessage message)
        {
            switch (message.message_type)
            {
                case MessageType.Input:
                    {
                        this.managers[message.task_id].StandardInput.Write(Athena.Utilities.Misc.Base64Decode(message.data) + Environment.NewLine);
                    }
                    break;
                case MessageType.CtrlA:
                    {
                        this.managers[message.task_id].TrySignalAsync(CommandSignal.FromSystemValue(0x01));
                    }
                    break;
                case MessageType.CtrlB:
                    {
                        this.managers[message.task_id].TrySignalAsync(CommandSignal.FromSystemValue(0x02));
                    }
                    break;
                case MessageType.CtrlC:
                    {
                        this.managers[message.task_id].TrySignalAsync(CommandSignal.FromSystemValue(0x03)); ;
                        //this.managers[message.task_id].WriteLineAsync("\x03");
                    }
                    break;
                case MessageType.CtrlD:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x04");
                    }
                    break;
                case MessageType.CtrlE:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x05");
                    }
                    break;
                case MessageType.CtrlF:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x06");
                    }
                    break;
                case MessageType.CtrlG:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x07");
                    }
                    break;
                case MessageType.Backspace:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x08");
                    }
                    break;
                case MessageType.Tab:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x09");
                    }
                    break;
                case MessageType.CtrlK:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x0B");
                    }
                    break;
                case MessageType.CtrlL:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x0C");
                    }
                    break;
                case MessageType.CtrlN:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x0E");
                    }
                    break;
                case MessageType.CtrlP:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x10");
                    }
                    break;
                case MessageType.CtrlQ:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x11");
                    }
                    break;
                case MessageType.CtrlR:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x12");
                    }
                    break;
                case MessageType.CtrlS:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x13");
                    }
                                        break;
                case MessageType.CtrlU:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x15");
                    }
                    break;
                case MessageType.CtrlW:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x17");
                    }
                    break;
                case MessageType.CtrlY:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x19");
                    }
                    break;
                case MessageType.CtrlZ:
                    {
                        //this.managers[message.task_id].WriteLineAsync("\x1A");
                    }
                    break;
                case MessageType.Exit:
                    {
                        //this.managers[message.task_id].Exit();
                    }
                    break;
                case MessageType.end:
                    {
                        //this.managers[message.task_id].Exit();
                    }
                    break;
                default:
                    break;
            }
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }
    }
}
