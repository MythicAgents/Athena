using Agent.Utilities;
using System.Net;
using System.Text;

namespace Agent.Models
{
    public class ConnectionOptions
    {
        public byte addressType { get; set; }
        public IPAddress? ip { get; set; }
        public int port { get; set; }
        public int server_id { get; set; }
        public string host { get; set; } = string.Empty;
        private readonly byte[] packetBytes;

        public ConnectionOptions(ServerDatagram sm)
        {
            server_id = sm.server_id;
            try
            {
                packetBytes = string.IsNullOrEmpty(sm.data)
                    ? Array.Empty<byte>()
                    : Misc.Base64DecodeToByteArray(sm.data);
            }
            catch
            {
                packetBytes = Array.Empty<byte>();
            }
        }

        public ConnectionOptions(int serverId, byte[] packetBytes)
        {
            server_id = serverId;
            this.packetBytes = packetBytes ?? Array.Empty<byte>();
        }

        public bool Parse()
        {
            if (packetBytes.Length < 4 || packetBytes[0] != 0x05 || packetBytes[1] != 0x01 || packetBytes[2] != 0x00)
                return false;

            addressType = packetBytes[3];
            int portOffset;
            switch ((AddressType)addressType)
            {
                case AddressType.IPv4:
                    if (packetBytes.Length != 10) return false;
                    ip = new IPAddress(packetBytes.AsSpan(4, 4));
                    host = ip.ToString();
                    portOffset = 8;
                    break;
                case AddressType.DomainName:
                    if (packetBytes.Length < 7) return false;
                    int domainLength = packetBytes[4];
                    if (domainLength == 0 || packetBytes.Length != 7 + domainLength) return false;
                    host = Encoding.ASCII.GetString(packetBytes, 5, domainLength);
                    portOffset = 5 + domainLength;
                    break;
                case AddressType.IPv6:
                    if (packetBytes.Length != 22) return false;
                    ip = new IPAddress(packetBytes.AsSpan(4, 16));
                    host = ip.ToString();
                    portOffset = 20;
                    break;
                default:
                    return false;
            }

            port = (packetBytes[portOffset] << 8) | packetBytes[portOffset + 1];
            return port != 0;
        }
    }
}
