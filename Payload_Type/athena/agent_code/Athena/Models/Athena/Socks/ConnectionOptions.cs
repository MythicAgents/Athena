using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Athena.Models.Athena.Socks
{
    public class ConnectionOptions
    {
        public byte addressType { get; set; }
        public IPAddress ip { get; set; }
        public byte[] dstportBytes { get; set; }
        public byte[] dstBytes { get; set; }
        public byte[] bndPortBytes { get; set; }
        public byte[] bndBytes { get; set; }
        public int port { get; set; } = 0;
        public int server_id { get; set; }

        public ConnectionOptions(SocksMessage sm)
        {
            byte[] packetBytes = Misc.Base64DecodeToByteArray(sm.data);
            this.addressType = packetBytes[3];
            this.dstBytes = GetDestinationBytes(packetBytes);
            this.dstportBytes = new byte[] { packetBytes[packetBytes.Length - 2], packetBytes[packetBytes.Length - 1] };
            this.ip = GetDestination(this.dstBytes, this.addressType);
            this.port = GetPort(packetBytes);
            this.server_id = sm.server_id;
        }
        
        private IPAddress GetDestination(byte[] destBytes, byte addressType)
        {
            try
            {
                //Check to see what type of destination value we have (IPv4, IPv6, or FQDN)
                switch (addressType)
                {
                    case (byte)0x01: //IPv4
                        return new IPAddress(destBytes.ToArray());
                    case (byte)0x03: //FQDN
                        IPAddress[] ipAddresses = Dns.GetHostEntry(Encoding.ASCII.GetString(destBytes)).AddressList;

                        //can maybe shorten this to return ipAddresses.FirstOrDefault() //Unsure how null will handle this though
                        if (ipAddresses.Count() > 0)
                        {
                            return ipAddresses[0];
                        }
                        else
                        {
                            return null;
                        }
                    case (byte)0x04: //IPv6
                        return new IPAddress(destBytes.ToArray());
                    default: //Fail
                        return null;
                }
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return null;
            }
        }
        private byte[] GetDestinationBytes(byte[] packetBytes)
        {
            try
            {
                //Can maybe conver this to a linq query
                List<byte> destBytes = new List<byte>();
                int packetBytesLength = packetBytes.Length;
                for (int i = 4; i < (packetBytes.Length - 2); i++)
                {
                    destBytes.Add(packetBytes[i]);
                }

                return destBytes.ToArray();
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return new byte[] { };
            }
        }
        private int GetPort(byte[] packetBytes)
        {
            try
            {
                //Get last two bytes of datagram, which contain the port in little endian format
                byte[] portBytes = new byte[] { packetBytes[packetBytes.Length - 2], packetBytes[packetBytes.Length - 1] };

                //Reverse for little endian
                Array.Reverse(portBytes);

                //Return the port
                return Convert.ToInt32(BitConverter.ToUInt16(portBytes));
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return 0;
            }
        }
    }
}
