using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            if (!args.ContainsKey("id") || String.IsNullOrEmpty(args["id"].ToString()))
            {
                return new PluginResponse()
                {
                    success = false,
                    output = "You need to specify a process to kill!"
                };
            }
            else
            {
                try
                {
                    Process proc = Process.GetProcessById((int)args["id"]);
                    proc.Kill();

                    int i = 0;
                    while (!proc.HasExited)
                    {
                        if (i == 10)
                        {
                            return new PluginResponse()
                            {
                                success = false,
                                output = "Process ID " + proc.Id + " did not exit in the alotted time."
                            };
                        }
                        Thread.Sleep(1000);
                        i++;
                    }

                    return new PluginResponse()
                    {
                        success = true,
                        output = "Process ID " + proc.Id + " killed."
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

        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }

}
