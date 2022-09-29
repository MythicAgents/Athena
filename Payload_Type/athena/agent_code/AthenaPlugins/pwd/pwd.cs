using Athena.Plugins;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "pwd";
        public override void Execute(Dictionary<string, object> args)
        {
            PluginHandler.AddResponse(new ResponseResult
            {
                completed = "true",
                user_output = Directory.GetCurrentDirectory(),
                task_id = (string)args["task-id"],
            });
        }
    }
}
