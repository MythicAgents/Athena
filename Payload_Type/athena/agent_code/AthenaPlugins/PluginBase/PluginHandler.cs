using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
namespace PluginBase
{
    public class PluginHandler
    {
        private static ConcurrentDictionary<string, object> responses = new ConcurrentDictionary<string, object>();

        public static void AddResponse(ResponseResult res)
        {
            if (responses.ContainsKey(res.task_id))
            {
                ResponseResult newResponse = (ResponseResult)responses[res.task_id];
                if (!string.IsNullOrEmpty(res.completed))
                {
                    newResponse.completed = res.completed;
                }

                if (!string.IsNullOrEmpty(res.status))
                {
                    newResponse.status = res.status;
                }
            }
            else
            {
                responses.TryAdd(res.task_id, res);
            }
        }

        public static void WriteOutput(string output, string task_id, bool completed, string status)
        {
            responses.AddOrUpdate(task_id, new ResponseResult { user_output = output, completed = completed.ToString(), status = status, task_id = task_id }, (k, t) =>
            {
                var newResponse = (ResponseResult)t;
                newResponse.user_output += output;
                if (completed)
                {
                    newResponse.completed = "true";
                }
                if (!string.IsNullOrEmpty(status))
                {
                    newResponse.status = status;
                }
                return newResponse;
            });
        }
        public static void WriteOutput(string output, string task_id, bool completed)
        {
            WriteOutput(output, task_id, completed, "");
        }

        public static async Task<List<object>> GetResponses()
        {
            if (responses.Values is null)
            {
                return new List<object>();
            }

            if (responses.Values.Count < 1)
            {
                return new List<object>();
            }

            List<object> results = new List<object>(responses.Values);
            Console.WriteLine("Sending: " + results.Count);
            responses.Clear();

            return results;
        }
    }
}
