using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Athena.Utilities;

namespace Athena.Models.Athena.Socks
{
    public class ConnectionOptions
    {
        public IPEndPoint endpoint { get; set; }
        public string fqdn { get; set; }
        public IPAddress ip { get; set; }
        public int port { get; set; } = 0;
        public byte[] dstportBytes { get; set; }
        public byte[] dstBytes { get; set; }
        public byte[] bndPortBytes { get; set; }
        public byte[] bndBytes { get; set; }
        public byte addressType { get; set; }
        public AddressFamily addressFamily { get; set; }
        public Socket socket { get; set; } 
        public int server_id { get; set; }

        public override string ToString()
        {
            string output = "";
            output += String.Format($"[IP] {this.ip.ToString()}") + Environment.NewLine;
            output += String.Format($"[FQDN] {this.fqdn}") + Environment.NewLine;
            output += String.Format($"[PORT] {this.port}") + Environment.NewLine;
            return output;
        }

        public ConnectionOptions(byte[] datagram, int server_id )
        {
            if (ParsePacket(datagram))
            {

                //Change this socket request based on datagram[1]
                if (this.addressFamily != AddressFamily.Unknown) { this.socket = GetSocket(); }
                if (this.socket != null) { 
                    this.endpoint = new IPEndPoint(this.ip, this.port);
                }
            }
            this.server_id = server_id;
        }
        private bool ParsePacket(byte[] packetBytes)
        {
            try
            {
                //Get the remote port from the packet
                this.port = GetPort(packetBytes);

                //Figure out the final destination
                List<byte> destBytes = new List<byte>();
                int packetBytesLength = packetBytes.Length;
                for (int i = 4; i < (packetBytesLength - 2); i++)
                {
                    destBytes.Add(packetBytes[i]);
                }

                this.dstBytes = destBytes.ToArray();
                this.addressType = packetBytes[3];

                //Check to see what type of destination value we have (IPv4, IPv6, or FQDN)
                switch (this.addressType)
                {
                    case (byte)0x01: //IPv4
                        this.ip = new IPAddress(destBytes.ToArray());
                        this.addressFamily = AddressFamily.InterNetwork;
                        return true;
                    case (byte)0x03: //FQDN

                        //Get DNS Results for the IP
                        this.fqdn = Encoding.ASCII.GetString(destBytes.ToArray());
                        IPAddress[] ipAddresses = Dns.GetHostEntry(this.fqdn).AddressList;

                        if (ipAddresses.Count() > 0)
                        {
                            //Get first IP result and the AddressFamily of that result.
                            this.ip = ipAddresses[0];
                            this.addressFamily = ipAddresses[0].AddressFamily;
                            return true;
                        }
                        else
                        {
                            //Couldn't resolve DNS
                            Misc.WriteDebug("DNS Failed.");
                            this.endpoint = null;
                            this.addressFamily = AddressFamily.Unknown;
                            return false;
                        }
                    case (byte)0x04: //IPv6
                        this.ip = new IPAddress(destBytes.ToArray());
                        this.addressFamily = AddressFamily.InterNetworkV6;
                        return true;
                    default: //Fail
                        this.addressFamily = AddressFamily.Unknown;
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private Socket GetSocket()
        {
            Socket s = null;
            try
            {
                IPEndPoint localEndPoint;
                switch (this.addressFamily)
                {
                    case AddressFamily.InterNetwork: //IPv4
                        {
                            localEndPoint = new IPEndPoint(IPAddress.Any, 0);
                            s = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                            s.Bind(localEndPoint);
                            this.bndBytes = IPAddress.Loopback.GetAddressBytes();
                            this.bndPortBytes = GetPortBytes((UInt16)((IPEndPoint)s.LocalEndPoint).Port);
                            return s;
                        }
                    case AddressFamily.InterNetworkV6: //IPv6
                        {
                            localEndPoint = new IPEndPoint(IPAddress.Any, 0);
                            s = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                            s.Bind(localEndPoint);
                            this.bndBytes = IPAddress.IPv6Loopback.GetAddressBytes();
                            this.bndPortBytes = GetPortBytes((UInt16)((IPEndPoint)s.LocalEndPoint).Port);
                            return s;
                        }
                    default:
                        {
                            return null;
                        }
                }
            }
            catch
            {
                return null;
            }
        }
        private int GetPort(byte[] packetBytes)
        {
            try
            {
                int packetBytesLength = packetBytes.Length;

                //Get last two bytes of datagram, which contain the port in little endian format
                byte[] portBytes = new byte[] { packetBytes[packetBytesLength - 2], packetBytes[packetBytesLength - 1] };
                this.dstportBytes = portBytes;

                //Reverse for little endian
                Array.Reverse(portBytes);

                //Return the port
                return Convert.ToInt32(BitConverter.ToUInt16(portBytes));
            }
            catch
            {
                return 0;
            }
        }
        private byte[] GetPortBytes(UInt16 port)
        {
            byte[] portBytes = BitConverter.GetBytes(port);
            if (BitConverter.IsLittleEndian)
            {
                return portBytes;
            }
            else
            {
                //Reverse that bitch
                return new byte[] { portBytes[1], portBytes[0] };
            }
        }
    }
}
