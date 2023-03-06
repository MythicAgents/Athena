using Athena.Models;
using Athena.Plugins;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plugins
{
    public class Pwd : AthenaPlugin
    {
        public override string Name => "pwd";
        public override void Execute(Dictionary<string, string> args)
        {
            PluginHandler.AddResponse(new ResponseResult
            {
                completed = "true",
                user_output = Directory.GetCurrentDirectory(),
                task_id = args["task-id"],
            });
        }
    }
}
