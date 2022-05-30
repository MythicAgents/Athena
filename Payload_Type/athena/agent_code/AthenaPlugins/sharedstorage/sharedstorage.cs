using PluginBase;
using System;
using System.Collections.Generic;
using System.Text;

namespace Plugin
{
    public static class sharedstorage
    {
        static int i = 0;
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                i++;

                //Return a successful response
                return new ResponseResult
                {
                    completed = "true",
                    user_output = $"Incremented current value: {i}",
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
