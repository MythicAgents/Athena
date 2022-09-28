using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using PluginBase;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "cd"; 
        public override void Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path") && !string.IsNullOrEmpty((string)args["path"]))
                {
                    string path = ((string)args["path"]).Replace("\"", "");

                    Directory.SetCurrentDirectory(path);

                    PluginHandler.Write($"Changed directory to {Directory.GetCurrentDirectory()}", (string)args["task-id"], true);
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
