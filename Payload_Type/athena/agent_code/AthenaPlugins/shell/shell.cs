using PluginBase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Plugin
{
    public static class Plugin
    {
        static ConcurrentDictionary<string, ShellJob> commandTracking = new ConcurrentDictionary<string, ShellJob>();
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                return ShellExec(args);
            }
            catch (Exception e)
            {
                //oh no an error
                return new ResponseResult
                {
                    completed = "true",
                    user_output = e.Message,
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
        }
        public static ResponseResult ShellExec(Dictionary<string, object> args)
        {
            string parameters = "";
            if (String.IsNullOrEmpty((string)args["parameters"]))
            {
                parameters = (string)args["parameters"];
            }

            string executable = (string)args["executable"];

            ShellJob sj = new ShellJob((string)args["task-id"])
            {
                sb = new StringBuilder(),
                isRunning = true,
                process = new Process
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
                }
            };
            try
            {
                sj.process.ErrorDataReceived += (sender, errorLine) => { if (errorLine.Data is not null) sj.sb.AppendLine(errorLine.Data); };
                sj.process.OutputDataReceived += (sender, outputLine) => { if (outputLine.Data is not null) sj.sb.AppendLine(outputLine.Data); };


                sj.process.Start();
                sj.process.BeginErrorReadLine();
                sj.process.BeginOutputReadLine();

                //Add to Tracking
                commandTracking.GetOrAdd(sj.task_id, sj);

                sj.process.WaitForExit();

                //Remove from tracking
                commandTracking.Remove(sj.task_id, out _);

                sj.isRunning = false;

                ResponseResult result = new ResponseResult()
                {
                    user_output = sj.sb.ToString(),
                    task_id = sj.task_id,
                    completed = "true",
                };

                sj.sb.Clear();

                if (sj.process.ExitCode != 0)
                {
                    result.status = "error";
                    result.user_output += Environment.NewLine + "Process exited with code: " + sj.process.ExitCode;
                }

                return result;
            }
            catch (Exception e)
            {
                return new ResponseResult()
                {
                    //user_output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd() + Environment.NewLine + e.Message,
                    user_output = sj.sb.ToString() + Environment.NewLine + e.Message,
                    task_id = sj.task_id,
                    completed = "true",
                    status = "error"
                };
            }
        }
        /// <summary>
        /// Get current output from executing commands
        /// </summary>
        public static List<ResponseResult> GetOutput()
        {
            ConcurrentBag<ResponseResult> results = new ConcurrentBag<ResponseResult>();

            Parallel.ForEachAsync(commandTracking, async (job, cancellationToken) =>
            {
                if (job.Value.sb.Length > 0)
                {
                    results.Add(new ResponseResult()
                    {
                        user_output = job.Value.sb.ToString(),
                        task_id = job.Value.task_id,
                    });

                    job.Value.sb.Clear();
                }
            });

            return results.ToList();
        }
    }
    public class ShellJob 
    {
        public StringBuilder sb { get; set; }
        public bool isRunning { get; set; }
        public Process process { get; set; }
        public string task_id { get; set; }


        public ShellJob(string task_id)
        {
            this.task_id = task_id;
        }
    }

}
