using System;
using System.Collections.Generic;
using System.IO;

namespace Athena
{
    public static class Plugin
    {

        public static string Execute(Dictionary<string, object> args)
        {
            string Output = "";

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
            {
                try
                {
                    string DriveName = "Name : " + d.Name + "";
                    var DriveType = "\tType : " + d.DriveType + "";
                    string DriveFreeSpace = "\tFree Space : " + d.TotalFreeSpace + "";
                    string DriveTotalSize = "\tTotal Size : " + d.TotalSize + "";
                    Output = Environment.NewLine + DriveName + Environment.NewLine + DriveType + Environment.NewLine + DriveTotalSize + Environment.NewLine + DriveFreeSpace  +Environment.NewLine;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    //Console.WriteLine("Device Is busy")
                }
            }
            return "Drives Info Found:" + Environment.NewLine + Output;
            //Add Stuff For drive size etc usage
            //Old code
            //foreach (var drive in Environment.GetLogicalDrives())
            //{
            //   Output += "\t" + drive + Environment.NewLine;
            //}

        }
    }
}
