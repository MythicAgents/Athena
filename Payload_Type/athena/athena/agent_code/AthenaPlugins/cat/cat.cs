using System;
using System.Collections.Generic;
using System.IO;
using Athena.Commands;
using Athena.Commands.Models;

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
                    TaskResponseHandler.Write(File.ReadAllText(args["path"].ToString().Replace("\"", "")), args["task-id"], true);
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

