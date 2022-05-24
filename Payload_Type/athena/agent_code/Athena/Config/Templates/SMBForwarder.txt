using Athena.Models.Mythic.Response;
using H.Pipes;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class Forwarder
    {
        public ConcurrentBag<DelegateMessage> messageOut { get; set; }
        private PipeClient<string> clientPipe { get; set; }
        public bool connected { get; set; }
        private object _lock = new object();

        public Forwarder()
        {
            this.messageOut = new ConcurrentBag<DelegateMessage>();
        }

        public List<DelegateMessage> GetMessages()
        {
            List<DelegateMessage> messagesOut;
            messagesOut = new List<DelegateMessage>(this.messageOut);
            this.messageOut.Clear();
            
            return messagesOut;
        }

        //Link to the Athena SMB Agent
        public async Task<bool> Link(string host, string pipename)
        {
            try
            {
                if (this.clientPipe is null || !this.connected)
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
        public async Task<bool> ForwardDelegateMessage(DelegateMessage dm)
        {
            try
            {
                await this.clientPipe.WriteAsync(JsonConvert.SerializeObject(dm));
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
        public async Task<bool> Unlink()
        {
            try
            {
                await this.clientPipe.DisconnectAsync();
                this.connected = false;
                await this.clientPipe.DisposeAsync();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
