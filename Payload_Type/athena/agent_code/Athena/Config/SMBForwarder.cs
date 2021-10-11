using Athena.Models.Athena.Pipes;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using H.Pipes;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class SMBForwarder
    {
        public bool connected { get; set; }
        public ConcurrentBag<DelegateMessage> messageOut { get; set; }
        public ConcurrentQueue<string> toAthena { get; set; } //Do I need this?
        private PipeClient<string> clientPipe { get; set; }

        public SMBForwarder()
        {
            this.messageOut = new ConcurrentBag<DelegateMessage>();
            this.toAthena = new ConcurrentQueue<string>();
        }

        public List<DelegateMessage> GetMessages()
        {
            List<DelegateMessage> messagesOut = new List<DelegateMessage>(this.messageOut.ToList());
            this.messageOut.Clear();
            return messagesOut;
        }


        //Link to the Athena SMB Agent
        public async Task<bool> Link(string host, string pipename)
        {
            try
            {
                if (this.clientPipe == null || !this.connected)
                {
                    this.clientPipe = new PipeClient<string>(pipename, host);
                    this.clientPipe.MessageReceived += (o, args) => AddMessageToQueue(args.Message);
                    this.clientPipe.Connected += (o, args) => this.connected = true;
                    this.clientPipe.Disconnected += (o, args) => this.connected = false;
                    await clientPipe.ConnectAsync();

                    if (clientPipe.IsConnected)
                    {
                        this.connected = true;
                        return true;
                    }
                    else { return false; }
                }
                else { return false; }
            }
            catch { return false; }
        }
        public bool ForwardDelegateMessage(DelegateMessage dm)
        {
            try
            {
                this.clientPipe.WriteAsync(JsonConvert.SerializeObject(dm));
                return true;
            }
            catch
            {
                return false;
            }
        }
        private void AddMessageToQueue(string message)
        {
            DelegateMessage dm = JsonConvert.DeserializeObject<DelegateMessage>(message);
            this.messageOut.Add(dm);
        }

        //Unlink from the named pipe
        public void Unlink()
        {

        }
    }
}
