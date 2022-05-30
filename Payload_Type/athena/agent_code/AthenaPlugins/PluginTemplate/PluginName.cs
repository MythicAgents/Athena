using PluginBase;
using System;
using System.Collections.Generic;
using System.Text;

namespace Plugin
{
    public static class Plugin
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                if (args.ContainsKey("myparameter"))
                {
                    sb.Append($"MyParameter: {(string)args["myparameter"]}");
                }

                if (args.ContainsKey("message"))
                {
                    sb.Append($"You wanted me to say: {(string)args["message"]}");
                }

                //Return a successful response
                return new ResponseResult
                {
                    completed = "true",
                    user_output = sb.ToString(),
                    task_id = (string)args["task-id"], //task-id passed in from Athena
                };
            }
            catch (Exception e)
            {
                //oh no an error
                return new ResponseResult
                {
                    completed = "true",
                    user_output = e.Message,
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
        }
    }

}
