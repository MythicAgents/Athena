using PluginBase;
using System.Collections.Generic;
using System.Net;
namespace Athena
{
    public static class Plugin
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            return new ResponseResult
            {
                completed = "true",
                user_output = Dns.GetHostName(),
                task_id = (string)args["task-id"],
            };
        }
    }
}
