

using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace mkdir
{
    public class Mkdir : IPlugin
    {
        public string Name => "mkdir";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }
        public Mkdir(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (args.ContainsKey("path"))
                {
                    DirectoryInfo dir = Directory.CreateDirectory((args["path"]).Replace("\"", ""));

                    await messageManager.AddResponse(new ResponseResult
                    {
                        completed = true,
                        user_output = "Created directory " + dir.FullName,
                        task_id = job.task.id,
                    });
                }
                else
                {
                    await messageManager.AddResponse(new ResponseResult
                    {
                        completed = true,
                        process_response = new Dictionary<string, string> { { "message", "0x2A" } },
                        task_id = job.task.id,
                        status = "error"
                    });
                }
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
    }
}
