using System.Collections.Generic;

namespace Athena.Models.Athena.Socks
{
    //https://en.wikipedia.org/wiki/SOCKS#SOCKS5
    public class ConnectResponse
    {
        byte version = 0x05;
        public ConnectResponseStatus status { get; set; }
        byte rsv = 0x00;
        public byte addrtype { get; set; }
        public byte[] bndaddr { get; set; }
        public byte[] bndport { get; set; }

        public byte[] ToByte()
        {
            List<byte> bytes = new List<byte>();

            bytes.Add(this.version);
            bytes.Add((byte)this.status);
            bytes.Add(this.rsv);
            bytes.Add(this.addrtype);
            foreach(var b in bndaddr)
            {
                bytes.Add(b);
            }
            foreach(var b in bndport)
            {
                bytes.Add(b);
            }
            return bytes.ToArray();
        }
    }

    public enum ConnectResponseStatus : byte
    {
        Success = 0x00,
        GeneralFailure = 0x01,
        ConnectionNotAllowed = 0x02,
        NetworkUnreachable = 0x03,
        HostUnreachable = 0x04,
        ConnectionRefused = 0x05,
        TTLExpired = 0x06,
        ProtocolError = 0x07,
        AddressTypeNotSupported = 0x08
    }
}
