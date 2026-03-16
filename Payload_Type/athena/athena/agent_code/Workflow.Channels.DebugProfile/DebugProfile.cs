using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;

namespace Workflow.Channels
{
    public class DebugProfile : IChannel
    {
        public IServiceConfig agentConfig { get; set; }
        public DateTime killDate { get; set; }
        public ISecurityProvider crypt { get; set; }
        private IDataBroker messageManager { get; set; }
        public bool encrypted { get; set; }
        public string? psk { get; set; }
        private ILogger logger { get; set; }
        private CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public event EventHandler<TaskingReceivedArgs>? SetTaskingReceived;

        public DebugProfile(IServiceConfig config, ISecurityProvider crypto, ILogger logger, IDataBroker messageManager)
        {
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;
        }


        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            DebugLog.Log("DebugProfile simulated checkin starting");
            await Task.Delay(5000);

            DebugLog.Log("DebugProfile simulated checkin complete");
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
                DebugLog.Log("DebugProfile beacon iteration starting");
                var taskResponses = messageManager.GetAgentResponseString();

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

                DebugLog.Log($"DebugProfile injecting task: {response.tasks[0].command}");
                TaskingReceivedArgs tra = new TaskingReceivedArgs(response);

                if(SetTaskingReceived is not null)
                {
                    SetTaskingReceived(this, tra);
                }

                await Task.Delay(Misc.GetSleep(agentConfig.sleep, agentConfig.jitter)*1000);
            }
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();

            return true;
        }
    }
}
