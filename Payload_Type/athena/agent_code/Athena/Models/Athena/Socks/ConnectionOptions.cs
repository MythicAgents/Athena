using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System;
using System.Linq;
using System.Net;
using System.Text;

namespace Athena.Models.Athena.Socks
{
    public class ConnectionOptions
    {
        public byte addressType { get; set; }
        public IPAddress ip { get; set; }
        public int port { get; set; } = 0;
        public int server_id { get; set; }

        public ConnectionOptions(SocksMessage sm)
        {
            this.server_id = sm.server_id;
            ParsePacket(Misc.Base64DecodeToByteArray(sm.data));
        }
       
        private void ParsePacket(byte[] packetBytes)
        {
            this.addressType = packetBytes[3];
            switch(this.addressType)
            {
                case 0x01: //IPv4
                    {
                        byte[] dstBytes = packetBytes.Skip(4).Take(4).ToArray();
                        this.port = (int)BitConverter.ToUInt16(packetBytes.Skip(8).Reverse().ToArray(), 0);
                        this.ip = new IPAddress(dstBytes);
                        break;
                    }
                case 0x03: //FQDN
                    {
                        int domainLength = packetBytes[4];
                        string domainName = Encoding.UTF8.GetString(packetBytes.Skip(5).Take(domainLength).ToArray());
                        this.port = (int)BitConverter.ToUInt16(packetBytes.Skip(5 + domainLength).Take(2).Reverse().ToArray(), 0);
                        try
                        {
                            this.ip = Dns.GetHostEntry(domainName).AddressList[0];
                        }
                        catch
                        {
                            this.ip = null;
                        }
                        break;
                    }
                case 0x04: //IPv6
                    {
                        byte[] dstBytes = packetBytes.Skip(4).Take(16).ToArray();
                        this.port = (int)BitConverter.ToUInt16(packetBytes.Skip(20).Reverse().ToArray(), 0);
                        this.ip = new IPAddress(dstBytes);
                        break;
                    }
                default:
                    break;
            }
        }
        public void PrintByteArray(byte[] bytes)
        {
            var sb = new StringBuilder("new byte[] { ");

            Array.ForEach(bytes, b => sb.Append(b + ", "));
            sb.Append("}");
            Console.WriteLine(sb.ToString());
        }
    }
}
