using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using PluginBase;

namespace Plugin
{
    public static class env
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            StringBuilder output = new StringBuilder();
            output.Append("[");
            foreach (DictionaryEntry fs in Environment.GetEnvironmentVariables())
            {
  
                output.Append($"{{\"Name\":\"{fs.Key.ToString().Replace(@"\", @"\\")}\",\"Value\":\"{fs.Value.ToString().Replace(@"\", @"\\")}\"}}" + ","); // Get values in JSON styling and escape \
            }
            output.Remove(output.Length-1,1); // remove extra Comma
            output.Append("]"); // add ending array
            
            return new ResponseResult
            {
                completed = "true",
                user_output = output.ToString(),
                task_id = (string)args["task-id"],
            };

        }

    }
}
