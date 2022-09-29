using Athena.Plugins;
using System.Collections.Concurrent;

namespace Athena.Plugins
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

        public static void Write(string? output, string task_id, bool completed, string status)
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
        public static void WriteLine(string? output, string task_id, bool completed, string status)
        {
            responses.AddOrUpdate(task_id, new ResponseResult { user_output = output + Environment.NewLine, completed = completed.ToString(), status = status, task_id = task_id }, (k, t) =>
            {
                var newResponse = (ResponseResult)t;
                newResponse.user_output += output + Environment.NewLine;
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
        public static void WriteLine(string? output, string task_id, bool completed)
        {
            WriteLine(output, task_id, completed, "");
        }

        public static void Write(string? output, string task_id, bool completed)
        {
            Write(output, task_id, completed, "");
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
            responses.Clear();

            return results;
        }

        ////For hot loading Forwarders
        //public static async Task<List<object>> GetDelegates()
        //{
            
        //}
        //public static bool AddDelegate()
        //{
        //}


        ////For hot loading Socks
        //public static async Task<List<object>> GetSocks()
        //{
        //}
        //public static bool AddSocksMessage()
        //{
        //}
    }
}
