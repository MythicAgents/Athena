using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                Process[] procs;
                //if (args.ContainsKey("computer"))
                //{
                //    output += "Getting processes for: " + (string)args["computer"] + Environment.NewLine;
                //    try
                //    {
                //        procs = Process.GetProcesses((string)args["computer"]);
                //    }
                //    catch (Exception e)
                //    {
                //        output += "An error occured while enumerating remote processes: " + e.Message;
                //        return new PluginResponse()
                //        {
                //            success = false,
                //            output = output
                //        };
                //    }

                //}
                //else
                //{
                procs = Process.GetProcesses().OrderBy(p => p.Id).ToArray();
                //}
                sb.Append("[");
                foreach (var proc in procs)
                {
                    //There doesn't seem to be any way to get process owner when using plain .NET
                    sb.Append($"{{\"process_id\":\"{proc.Id}\",\"name\":\"{proc.ProcessName}\",\"title\":\"{proc.MainWindowTitle.Replace(@"\", @"\\")}\"}},");
                }

                sb.Remove(sb.Length - 1, 1);
                sb.Append("]");
                return new PluginResponse()
                {
                    success = true,
                    output = sb.ToString()
                };
            }
            catch (Exception e)
            {
                return new PluginResponse()
                {
                    success = false,
                    output = e.Message
                };
            }
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
