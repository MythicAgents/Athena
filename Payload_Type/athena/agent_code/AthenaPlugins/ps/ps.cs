using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            try
            {
                string output = "";
                Process[] procs;
                if (args.ContainsKey("computer"))
                {
                    output += "Getting processes for: " + (string)args["computer"] + Environment.NewLine;
                    try
                    {
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
                    procs = Process.GetProcesses().OrderBy(p => p.Id).ToArray();
                }
                output = "[";

                foreach (var proc in procs)
                {
                    //There doesn't seem to be any way to get process owner when using plain .NET
                    //output += proc.Id + "\t\t" + proc.ProcessName + "\t\t" + Environment.NewLine;
                    output += $"{{\"id\":\"{proc.Id}\",\"name\":\"{proc.ProcessName}\"}},";
                }

                output = output.TrimEnd(',');
                output += "]";
                return new PluginResponse()
                {
                    success = true,
                    output = output
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
