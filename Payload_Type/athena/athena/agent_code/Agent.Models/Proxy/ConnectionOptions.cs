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
            : this(sm.server_id, DecodePacket(sm.data))
        {
        }

        public ConnectionOptions(int serverId, byte[] packetBytes)
        {
            server_id = serverId;
            this.packetBytes = packetBytes ?? Array.Empty<byte>();
        }

        private static byte[] DecodePacket(string? data)
        {
            if (string.IsNullOrEmpty(data))
                return Array.Empty<byte>();

            try
            {
                return Misc.Base64DecodeToByteArray(data);
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        public bool Parse()
        {
            if (packetBytes.Length < 4 || packetBytes[0] != 0x05 || packetBytes[1] != 0x01 || packetBytes[2] != 0x00)
                return false;

            addressType = packetBytes[3];
            bool parsed = (AddressType)addressType switch
            {
                AddressType.IPv4 => TryParseIpAddress(4, out int ipv4PortOffset) && TryReadPort(ipv4PortOffset),
                AddressType.DomainName => TryParseDomainName(out int domainPortOffset) && TryReadPort(domainPortOffset),
                AddressType.IPv6 => TryParseIpAddress(16, out int ipv6PortOffset) && TryReadPort(ipv6PortOffset),
                _ => false
            };

            return parsed;
        }

        private bool TryParseIpAddress(int addressLength, out int portOffset)
        {
            portOffset = 4 + addressLength;
            if (packetBytes.Length != portOffset + 2)
                return false;

            ip = new IPAddress(packetBytes.AsSpan(4, addressLength));
            host = ip.ToString();
            return true;
        }

        private bool TryParseDomainName(out int portOffset)
        {
            portOffset = 0;
            if (packetBytes.Length < 7)
                return false;

            int domainLength = packetBytes[4];
            if (domainLength == 0 || packetBytes.Length != 7 + domainLength)
                return false;

            host = Encoding.ASCII.GetString(packetBytes, 5, domainLength);
            portOffset = 5 + domainLength;
            return true;
        }

        private bool TryReadPort(int portOffset)
        {
            port = (packetBytes[portOffset] << 8) | packetBytes[portOffset + 1];
            return port != 0;
        }
    }
}
