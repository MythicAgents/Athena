namespace Agent.Models
{
    //https://en.wikipedia.org/wiki/SOCKS#SOCKS5
    public class ConnectResponse
    {
        byte version = 0x05;
        public ConnectResponseStatus status { get; set; }
        byte rsv = 0x00;
        public byte addrtype { get; set; }
        public byte[] bndaddr { get; set; } = new byte[] { 0x01, 0x00, 0x00, 0x7F };
        public byte[] bndport { get; set; } = new byte[] { 0x00, 0x00 };

        public byte[] ToByte() => [version, (byte)status, rsv, addrtype, .. bndaddr, .. bndport];
    }
}
