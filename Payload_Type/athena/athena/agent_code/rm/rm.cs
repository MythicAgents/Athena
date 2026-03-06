using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using rm;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "rm";
        private IDataBroker messageManager { get; set; }
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            RmArgs args = JsonSerializer.Deserialize<RmArgs>(job.task.parameters);

            if(!args.Validate(out string message))
            {
                messageManager.Write(message, job.task.id, true, "error");
                return;
            }   

            try
            {
                FileAttributes attr = File.GetAttributes(args.path);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    Directory.Delete(args.path.Replace("\"", ""), true);
                }
                else
                {
                    File.Delete(args.path.Replace("\"", ""));
                }
                messageManager.Write($"{args.path} removed.", job.task.id, true);

            }
            catch (Exception e)
            {

                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
    }
}
