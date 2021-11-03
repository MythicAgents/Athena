using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
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
                    return new PluginResponse()
                    {
                        success = false,
                        output = output.ToString()
                    };
                }
            }
            output.Remove(output.Length - 1, 1);
            output.Append("]");
            return new PluginResponse()
            {
                success = true,
                output = output.ToString()
            };

        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
