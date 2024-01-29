using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using farmer;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "farmer";
        //public static IMessageManager messageManager { get; set; }
        private FarmerServer farm;
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private bool running = false;
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.logger = logger;
            //Plugin.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            FarmerArgs args = JsonSerializer.Deserialize<FarmerArgs>(job.task.parameters);

            if (!running)
            {
                try
                {
                    farm = new FarmerServer(this.logger, messageManager, job.task.id);

                    farm.Initialize(args.port);
                    await messageManager.Write($"Started farmer on port: {args.port}", job.task.id, false);
                    this.running = true;
                }
                catch (Exception e)
                {
                    await messageManager.Write($"Failed to start: {e}", job.task.id, false, "error");
                    this.running = false;
                }
            }
            else
            {
                farm.Stop();
                this.running = false;
            }
        }
    }
}
