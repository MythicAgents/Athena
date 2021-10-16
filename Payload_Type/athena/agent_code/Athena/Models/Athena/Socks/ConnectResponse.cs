using Athena.Utilities;
using System;
using System.Collections.Generic;

namespace Athena.Models.Athena.Socks
{
    //https://en.wikipedia.org/wiki/SOCKS#SOCKS5
    public class ConnectResponse
    {
        byte version = 0x05;
        public ConnectResponseStatus status { get; set; }
        byte rsv = 0x00;
        public byte addrtype { get; set; } = 0x01;
        public byte[] bndaddr { get; set; } = new byte[] { 0x01, 0x00, 0x00, 0x7F };
        public byte[] bndport { get; set; } = new byte[] { 0x00, 0x00 };

        public byte[] ToByte()
        {
            try
            {
                List<byte> bytes = new List<byte>();

                bytes.Add(this.version);
                bytes.Add((byte)this.status);
                bytes.Add(this.rsv);
                bytes.Add(this.addrtype);
                foreach (var b in bndaddr)
                {
                    bytes.Add(b);
                }
                foreach (var b in bndport)
                {
                    bytes.Add(b);
                }
                return bytes.ToArray();
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return new byte[] { 0x05, (byte)ConnectResponseStatus.GeneralFailure, 0x1, 0x01, 0x00, 0x00, 0x7F, 0x00, 0x00 };
            }
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
