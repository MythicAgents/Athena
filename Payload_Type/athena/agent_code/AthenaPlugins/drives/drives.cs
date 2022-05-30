using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PluginBase;

namespace Athena
{
    public static class drives
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            StringBuilder output = new StringBuilder();
            output.Append("[");
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
            {
                try
                {
                    output.Append($"{{\"DriveName\":\"{d.Name.Replace(@"\", @"\\")}\",\"DriveType\":\"{d.DriveType}\",\"FreeSpace\":\"{d.TotalFreeSpace.ToString()}\",\"TotalSpace\":\"{d.TotalSize.ToString()}\"}},");
                }
                catch(IOException e)
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
            
            return new ResponseResult
            {
                completed = "true",
                user_output = output.ToString(),
                task_id = (string)args["task-id"],
            };

        }
    }
}
