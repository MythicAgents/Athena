using System;
using System.Collections.Generic;
using PluginBase;

namespace Plugin
{
    public static class whoami
    {
        public static void Execute(Dictionary<string, object> args)
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
