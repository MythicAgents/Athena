using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;

namespace Athena
{
    public static class Plugin
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            return new ResponseResult
            {
                completed = "true",
                user_output = Directory.GetCurrentDirectory(),
                task_id = (string)args["task-id"],
            };
        }
    }
}
