using PluginBase;
using System.Collections.Generic;
using System.Net;
namespace Plugin
{
    public static class hostname
    {
        public static void Execute(Dictionary<string, object> args)
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
