using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;

namespace Agent.Profiles
{
    public class DebugProfile : IProfile
    {
        public IAgentConfig agentConfig { get; set; }
        public DateTime killDate { get; set; }
        public ICryptoManager crypt { get; set; }
        private IMessageManager messageManager { get; set; }
        public bool encrypted { get; set; }
        public string? psk { get; set; }
        private ILogger logger { get; set; }
        private CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public event EventHandler<TaskingReceivedArgs>? SetTaskingReceived;

        public DebugProfile(IAgentConfig config, ICryptoManager crypto, ILogger logger, IMessageManager messageManager)
        {
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;
        }


        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            Thread.Sleep(5000);

            return new CheckinResponse()
            {
                status = "success",
                id = Guid.NewGuid().ToString(),
                action = "checkin",
                encryption_key = "",
                decryption_key = ""
            };
        }

        public async Task StartBeacon()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var taskResponses = await messageManager.GetAgentResponseStringAsync();

                string fileGuid = Guid.NewGuid().ToString();
                Dictionary<string, string> smbParams = new Dictionary<string, string>()
                {
                    {"action","link" },
                    {"pipename","scottie_pipe" },
                    {"hostname", "127.0.0.1" }
                };

                var response = new GetTaskingResponse()
                {
                    action = "get_tasking",
                    tasks = new List<ServerTask> { 
                        new ServerTask()
                        {
                            command = "smb",
                            id = fileGuid,
                            parameters = JsonSerializer.Serialize(smbParams),
                            token = 0,
                        }
                    }
                };

                TaskingReceivedArgs tra = new TaskingReceivedArgs(response);

                if(SetTaskingReceived is not null)
                {
                    SetTaskingReceived(this, tra);
                }

                Thread.Sleep(Misc.GetSleep(agentConfig.sleep, agentConfig.jitter)*1000);
            }
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();

            return true;
        }
    }
}
