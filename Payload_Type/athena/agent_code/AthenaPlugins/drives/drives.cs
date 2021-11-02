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

                    string DriveName = d.Name;
                    var DriveType = d.DriveType;
                    string DriveFreeSpace = d.TotalFreeSpace.ToString();
                    string DriveTotalSize = d.TotalSize.ToString();
                    //output = Environment.NewLine + DriveName + Environment.NewLine + DriveType + Environment.NewLine + DriveTotalSize + Environment.NewLine + DriveFreeSpace  +Environment.NewLine;
                    //  output1.Append($"{{\"Name\":\"{fs.Key.ToString().Replace(@"\", @"\\")}\",\"Value\":\"{fs.Value.ToString().Replace(@"\", @"\\")}\"}}" + ","); // Get values in JSON styling and escape \
                    //output1.Append($"{{\"DriveName\":\"{DriveName.ToString().Replace(@"\", @"\\")}\",\"Type\":\"{DriveType.ToString().Replace(@"\", @"\\")}\"}}" + ","); // Get values in JSON styling and escape \
                    //output2.Append($"{{\"FreeSpace\":\"{DriveFreeSpace.ToString().Replace(@"\", @"\\")}\",\"TotalSize\":\"{DriveTotalSize.ToString().Replace(@"\", @"\\")}\"}}" + ","); // Get values in JSON styling and escape \
                    //output.Append($"{{\"DriveName\":\"{DriveName.ToString().Replace(@"\", @"\\")}\",\"Type\":\"{DriveType.ToString().Replace(@"\", @"\\")}\"}}" +
                    //  "FreeSpace\":\"{DriveFreeSpace.ToString().Replace(@"\", @"\\")}\",\"TotalSize\":\"{DriveTotalSize.ToString().Replace(@"\", @"\\")}\" + ", "); // Get values in JSON styling and escape \
                    output.Append($"{{\"DriveName\":\"{DriveName.Replace(@"\", @"\\")}\",\"DriveType\":\"{DriveType}\",\"FreeSpace\":\"{DriveFreeSpace}\",\"TotalSpace\":\"{DriveFreeSpace}\"}},");
                }
                //add , comma deleter at end and add start and []
                catch (Exception e)
                {
                    output.Append($"{{\"DriveName\":\"{d.Name.Replace(@"\", @"\\")}\",\"DriveType\":\"{e.Message.Split(":")[0].TrimEnd(' ')}\",\"FreeSpace\":\"\",\"TotalSpace\":\"\"}},");

                    output.Remove(output.Length - 1, 1); // remove extra Comma
                    output.Append("]"); // add endin
                    return new PluginResponse()
                    {
                        success = false,
                        output = output.ToString()
                    };
                    //Console.WriteLine("Device Is busy")
                }
            }
            return new PluginResponse()
            {
                success = true,
                output = output.ToString()
            };
            //Add Stuff For drive size etc usage
            //Old code
            //foreach (var drive in Environment.GetLogicalDrives())
            //{
            //   Output += "\t" + drive + Environment.NewLine;
            //}

        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
