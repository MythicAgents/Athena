using System;
using System.Collections.Generic;
using PluginBase;

namespace Athena
{
    public static class whoami
    {
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            return new ResponseResult()
            {
                task_id = (string)args["task-id"],
                user_output = $"{Environment.UserDomainName}\\{Environment.UserName}",
                completed = "true"
            };
        }
    }
}
