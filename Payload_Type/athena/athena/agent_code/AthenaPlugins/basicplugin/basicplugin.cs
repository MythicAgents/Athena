using Athena.Commands;
using Athena.Commands.Models;
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
                TaskResponseHandler.Write("Hello World", args["task-id"], true, "error");
            }
            catch (Exception e)
            {
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }
    }
}
