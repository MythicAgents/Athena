using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Diagnostics;

namespace Agent
{
    public sealed class ProcessRunner : IDisposable
    {
        private readonly Process process;
        private readonly string taskId;
        private readonly IMessageManager messageManager;
        private readonly Action onCompleted;
        private CancellationTokenRegistration cancellationRegistration;
        private int completed;

        public ProcessRunner(string command, string taskId, IMessageManager messageManager, Action? onCompleted = null)
        {
            this.messageManager = messageManager;
            this.taskId = taskId;
            this.onCompleted = onCompleted ?? (() => { });
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = string.Empty
                },
                EnableRaisingEvents = true
            };
        }

        public void Start(CancellationToken cancellationToken = default)
        {
            process.ErrorDataReceived += (_, line) => SendOutput(line.Data);
            process.OutputDataReceived += (_, line) => SendOutput(line.Data);
            try
            {
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                CancellationTokenRegistration registration = cancellationToken.Register(Stop);
                cancellationRegistration = registration;
                if (Volatile.Read(ref completed) != 0)
                {
                    registration.Unregister();
                }
                _ = MonitorExitAsync();
            }
            catch (Exception exception)
            {
                Complete("Process failed to start: " + exception.Message, "error");
            }
        }

        public void Stop()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    process.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
                // The process either never started or exited concurrently.
            }
            finally
            {
                Complete(Environment.NewLine + "Process Finished.", GetExitStatus());
            }
        }

        public void Write(byte[] input) => process.StandardInput.Write(input);
        public void Write(string input) => process.StandardInput.WriteLine(input);
        public void Write(byte input) => process.StandardInput.Write(input);

        public void Dispose()
        {
            process.Dispose();
        }

        private void SendOutput(string? output)
        {
            if (output is null) return;

            messageManager.AddInteractMessage(new InteractMessage
            {
                data = Misc.Base64Encode(output + Environment.NewLine),
                task_id = taskId,
                message_type = InteractiveMessageType.Output
            });
        }

        private async Task MonitorExitAsync()
        {
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                process.WaitForExit();
                Complete(Environment.NewLine + "Process Finished.", GetExitStatus());
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private string GetExitStatus()
        {
            try
            {
                return process.HasExited && process.ExitCode == 0 ? "success" : "error";
            }
            catch (InvalidOperationException)
            {
                return "error";
            }
        }

        private void Complete(string output, string status)
        {
            if (Interlocked.Exchange(ref completed, 1) != 0) return;

            cancellationRegistration.Unregister();
            onCompleted();
            messageManager.AddTaskResponse(new TaskResponse
            {
                user_output = output,
                task_id = taskId,
                completed = true,
                status = status
            });
            process.Dispose();
        }
    }
}
