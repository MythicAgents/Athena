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
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.logger = context.Logger;
            //Plugin.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            FarmerArgs args = JsonSerializer.Deserialize<FarmerArgs>(job.task.parameters);

            if (!running)
            {
                try
                {
                    DebugLog.Log($"{Name} starting server on port {args.port} [{job.task.id}]");
                    farm = new FarmerServer(this.logger, messageManager, job.task.id);

                    farm.Initialize(args.port);
                    messageManager.Write($"Started farmer on port: {args.port}", job.task.id, false);
                    this.running = true;
                    DebugLog.Log($"{Name} server started [{job.task.id}]");
                }
                catch (Exception e)
                {
                    DebugLog.Log($"{Name} failed to start: {e.Message} [{job.task.id}]");
                    messageManager.Write($"Failed to start: {e}", job.task.id, false, "error");
                    this.running = false;
                }
            }
            else
            {
                if(farm is null){
                    DebugLog.Log($"{Name} farm is null, returning [{job.task.id}]");
                    return;
                }

                DebugLog.Log($"{Name} stopping server [{job.task.id}]");
                farm.Stop();
                this.running = false;
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
