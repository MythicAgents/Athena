using Workflow.Utilities;
using System.Net;
using System.Text;

namespace Workflow.Models
{
    public class ConnectionOptions
    {
        public byte addressType { get; set; }
        public IPAddress ip { get; set; }
        public int port { get; set; } = 0;
        public int server_id { get; set; }
        public string host { get; set; }
        private byte[] packetBytes { get; set; }

        public ConnectionOptions(ServerDatagram sm)
        {
            this.server_id = sm.server_id;
            this.packetBytes = Misc.Base64DecodeToByteArray(sm.data);
        }
       
        public async Task<bool> ParseAsync()
        {
            this.addressType = this.packetBytes[3];
            switch((AddressType)this.addressType)
            {
                case AddressType.IPv4:
                    {
                        byte[] dstBytes = this.packetBytes.Skip(4).Take(4).ToArray();
                        this.port = (int)BitConverter.ToUInt16(this.packetBytes.Skip(8).Take(2).Reverse().ToArray(), 0);
                        this.ip = new IPAddress(dstBytes);
                        this.host = this.ip.ToString();
                        return true;
                    }
                case AddressType.DomainName:
                    {
                        int domainLength = this.packetBytes[4];
                        string domainName = Encoding.UTF8.GetString(this.packetBytes.Skip(5).Take(domainLength).ToArray());
                        this.port = (int)BitConverter.ToUInt16(this.packetBytes.Skip(5 + domainLength).Take(2).Reverse().ToArray(), 0);

                        IPHostEntry hosts = await Dns.GetHostEntryAsync(domainName).ConfigureAwait(false);
                        this.host = domainName;
                        if(hosts.AddressList.Length > 0)
                        {
                            this.ip = hosts.AddressList[0];
                            return true;
                        }
                        return false;
                    }
                case AddressType.IPv6:
                    {
                        byte[] dstBytes = this.packetBytes.Skip(4).Take(16).ToArray();
                        this.port = (int)BitConverter.ToUInt16(this.packetBytes.Skip(20).Take(2).Reverse().ToArray(), 0);
                        this.ip = new IPAddress(dstBytes);
                        this.host = this.ip.ToString();
                        return true;
                    }
                default:
                    return false;
            }
        }
    }
}
