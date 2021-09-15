using System;
using System.Collections.Generic;
using System.IO;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            string output = "";

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
            {
                try
                {
                    string DriveName = "Name : " + d.Name + "";
                    var DriveType = "\tType : " + d.DriveType + "";
                    string DriveFreeSpace = "\tFree Space : " + d.TotalFreeSpace + "";
                    string DriveTotalSize = "\tTotal Size : " + d.TotalSize + "";
                    output = Environment.NewLine + DriveName + Environment.NewLine + DriveType + Environment.NewLine + DriveTotalSize + Environment.NewLine + DriveFreeSpace  +Environment.NewLine;
                }
                catch (Exception e)
                {
                    output += Environment.NewLine + e.Message;
                    return new PluginResponse()
                    {
                        success = false,
                        output = output
                    };
                    //Console.WriteLine("Device Is busy")
                }
            }
            return new PluginResponse()
            {
                success = true,
                output = output
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
