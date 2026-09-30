using Agent.Interfaces;
using System.Runtime.InteropServices;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "caffeinate";
        private readonly IMessageManager messageManager;
        private readonly Action pulse;
        private readonly Func<CancellationToken, Task> delay;
        private readonly object stateLock = new();
        private CancellationTokenSource? activeCancellation;
        private Task? activeRun;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const int VK_F15 = 0x7E;
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;

        private static void ReleaseKey(byte keyCode) =>
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
            : this(messageManager, () => ReleaseKey(VK_F15), token => Task.Delay(TimeSpan.FromSeconds(59), token))
        {
        }

        internal Plugin(IMessageManager messageManager, Action pulse, Func<CancellationToken, Task> delay)
        {
            this.messageManager = messageManager;
            this.pulse = pulse;
            this.delay = delay;
        }

        public async Task Execute(ServerJob job)
        {
            CancellationTokenSource? cancellationToStop;
            Task? runToStop;

            lock (stateLock)
            {
                cancellationToStop = activeCancellation;
                runToStop = activeRun;
                if (cancellationToStop is null)
                {
                    var cancellation = new CancellationTokenSource();
                    activeCancellation = cancellation;
                    activeRun = RunAsync(job.task.id, cancellation);
                    runToStop = activeRun;
                }
                else
                {
                    cancellationToStop.Cancel();
                }
            }

            if (cancellationToStop is not null)
            {
                if (runToStop is not null)
                    await runToStop.ConfigureAwait(false);
                messageManager.WriteLine("Letting computer sleep", job.task.id, true);
                return;
            }

            await runToStop!.ConfigureAwait(false);
        }

        private async Task RunAsync(string taskId, CancellationTokenSource cancellation)
        {
            messageManager.WriteLine("Keeping PC awake", taskId, false);
            try
            {
                while (true)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    pulse();
                    await delay(cancellation.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                messageManager.WriteLine(exception.ToString(), taskId, true, "error");
                return;
            }
            finally
            {
                lock (stateLock)
                {
                    if (ReferenceEquals(activeCancellation, cancellation))
                    {
                        activeCancellation = null;
                        activeRun = null;
                    }
                }
                cancellation.Dispose();
            }

            messageManager.WriteLine("Done.", taskId, true);
        }
    }
}
