using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using farmer;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "farmer";
        //public static IDataBroker messageManager { get; set; }
        private FarmerServer? farm;
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private bool running = false;
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
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
                    messageManager.Write($"Started farmer on port: {args.port}", job.task.id, false);
                    this.running = true;
                }
                catch (Exception e)
                {
                    messageManager.Write($"Failed to start: {e}", job.task.id, false, "error");
                    this.running = false;
                }
            }
            else
            {
                if(farm is null){
                    return;
                }
                
                farm.Stop();
                this.running = false;
            }
        }
    }
}
