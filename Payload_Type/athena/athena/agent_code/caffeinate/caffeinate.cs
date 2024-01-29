using Agent.Interfaces;
using System.Runtime.InteropServices;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "caffeinate";
        private IMessageManager messageManager { get; set; }
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
        private const int VK_F15 = 0x7E;
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private static bool running = false;
        private CancellationTokenSource cts = new CancellationTokenSource();

        private static void PressKey(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, 0);
        }

        private static void ReleaseKey(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }


        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {

            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (running)
                {
                    cts.Cancel();
                    await messageManager.WriteLine("Letting computer sleep", job.task.id, true);

                }
                else
                {
                    await messageManager.WriteLine("Keeping PC awake", job.task.id, false);
                    running = true;
                    while (!cts.IsCancellationRequested)
                    {
                        //PressKey(VK_F15);
                        ReleaseKey(VK_F15);
                        Thread.Sleep(59000); // Press the key every 59 seconds
                    }
                    await messageManager.WriteLine("Done.", job.task.id, true);
                }
            }
            catch (Exception e)
            {
                await messageManager.WriteLine(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
