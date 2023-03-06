using Athena.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
namespace Plugins
{
    public class BasicPlugin : AthenaPlugin
    {
        public override string Name => "myplugin";
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                PluginHandler.Write("Hello World", args["task-id"], true, "error");
            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }
    }
}
