using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Athena.Commands;
using Athena.Commands.Models;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Caffeinate : IPlugin
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        public string Name => "caffeinate";

        public bool Interactive => false;

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

        public void Start(Dictionary<string, string> args)
        {
            try
            {
                if (running)
                {
                    running = false;
                    TaskResponseHandler.Write("Letting computer sleep", args["task-id"], true);
                    
                }
                else
                {
                    TaskResponseHandler.Write("Keeping PC awake", args["task-id"], true);
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
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }
    }
}
