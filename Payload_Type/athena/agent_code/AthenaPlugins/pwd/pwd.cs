using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plugin
{
    public static class pwd
    {

        public static void Execute(Dictionary<string, object> args)
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
