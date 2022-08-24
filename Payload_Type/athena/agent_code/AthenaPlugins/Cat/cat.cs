using System;
using System.Collections.Generic;
using System.IO;
using PluginBase;

namespace Plugin
{
    public static class cat
    {
        public static void Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    PluginHandler.WriteOutput(File.ReadAllText(args["path"].ToString().Replace("\"", "")), (string)args["task-id"], true);
                }
                else
                {
                    PluginHandler.WriteOutput("Missing path parameter", (string)args["task-id"], true, "error");
                }
            }
            catch (Exception e)
            {
                PluginHandler.WriteOutput(e.ToString(), (string)args["task-id"], true, "error");
            }
        }
    }

}

