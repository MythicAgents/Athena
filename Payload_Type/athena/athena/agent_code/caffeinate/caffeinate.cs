using Agent.Interfaces;

using Agent.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Agent.Models;
using Agent.Utilities;

namespace caffeinate
{
    public class Caffeinate : IPlugin
    {
        public string Name => "caffeinate";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
        private const int VK_F15 = 0x7E;
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private static bool running = false;


        private static void PressKey(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, 0);
        }

        private static void ReleaseKey(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }


        public Caffeinate(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (running)
                {
                    running = false;
                    messageManager.Write("Letting computer sleep", job.task.id, true);

                }
                else
                {
                    messageManager.Write("Keeping PC awake", job.task.id, true);
                    running = true;
                    while (running)
                    {
                        //PressKey(VK_F15);
                        ReleaseKey(VK_F15);
                        Thread.Sleep(59000); // Press the key every 59 seconds
                    }
                }
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
