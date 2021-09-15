using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            string output = "";
            Process[] procs;
            if (args.ContainsKey("computer"))
            {
                output += "Getting processes for: " + (string)args["computer"] + Environment.NewLine;
                try { 
                    procs = Process.GetProcesses((string)args["computer"]);
                }
                catch (Exception e)
                {
                    output += "An error occured while enumerating remote processes: " + e.Message;
                    return new PluginResponse()
                    {
                        success = false,
                        output = output
                    };
                }

            }
            else
            {
                output += "Getting local processes" + Environment.NewLine;
                procs = Process.GetProcesses();

            }
            foreach (var proc in procs)
            {
                output += proc.Id + "\t" + proc.ProcessName + "\t" + proc.MainWindowTitle;
            }
            return new PluginResponse()
            {
                success = true,
                output = output
            };
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
