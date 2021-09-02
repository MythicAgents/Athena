using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Commands.Model
{
    public class Socks
    {
        public Socks()
        {
            
        }

        public void Start()
        {

        }

        public async void Send()
        {

        }

        private byte[] GetDestPortBytes(int value)
        {
            return new byte[2]
            {
                Convert.ToByte(value / 256),
                Convert.ToByte(value % 256)
            };
        }
        private byte GetDestAddressType(string host)
        {
            if (!IPAddress.TryParse(host, out var ipAddr))
                return Socks5Constants.AddrtypeDomainName;

            switch (ipAddr.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    return Socks5Constants.AddrtypeIpv4;
                case AddressFamily.InterNetworkV6:
                    return Socks5Constants.AddrtypeIpv6;
                default:
                    throw new Exception(
                        string.Format("The host addess {0} of type '{1}' is not a supported address type.\n" +
                        "The supported types are InterNetwork and InterNetworkV6.", host,
                        Enum.GetName(typeof(AddressFamily), ipAddr.AddressFamily)));
            }

        }

        private byte[] GetDestAddressBytes(byte addressType, string host)
        {
            switch (addressType)
            {
                case Socks5Constants.AddrtypeIpv4:
                case Socks5Constants.AddrtypeIpv6:
                    return IPAddress.Parse(host).GetAddressBytes();
                case Socks5Constants.AddrtypeDomainName:
                    byte[] bytes = new byte[host.Length + 1];
                    bytes[0] = Convert.ToByte(host.Length);
                    Encoding.ASCII.GetBytes(host).CopyTo(bytes, 1);
                    return bytes;
                default:
                    return null;
            }
        }
    }
    public class Socks5Constants
    {

        public const byte Reserved = 0x00;
        public const byte AuthNumberOfAuthMethodsSupported = 2;
        public const byte AuthMethodNoAuthenticationRequired = 0x00;
        public const byte AuthMethodGssapi = 0x01;
        public const byte AuthMethodUsernamePassword = 0x02;
        public const byte AuthMethodIanaAssignedRangeBegin = 0x03;
        public const byte AuthMethodIanaAssignedRangeEnd = 0x7f;
        public const byte AuthMethodReservedRangeBegin = 0x80;
        public const byte AuthMethodReservedRangeEnd = 0xfe;
        public const byte AuthMethodReplyNoAcceptableMethods = 0xff;
        public const byte CmdConnect = 0x01;
        public const byte CmdBind = 0x02;
        public const byte CmdUdpAssociate = 0x03;
        public const byte CmdReplySucceeded = 0x00;
        public const byte CmdReplyGeneralSocksServerFailure = 0x01;
        public const byte CmdReplyConnectionNotAllowedByRuleset = 0x02;
        public const byte CmdReplyNetworkUnreachable = 0x03;
        public const byte CmdReplyHostUnreachable = 0x04;
        public const byte CmdReplyConnectionRefused = 0x05;
        public const byte CmdReplyTtlExpired = 0x06;
        public const byte CmdReplyCommandNotSupported = 0x07;
        public const byte CmdReplyAddressTypeNotSupported = 0x08;
        public const byte AddrtypeIpv4 = 0x01;
        public const byte AddrtypeDomainName = 0x03;
        public const byte AddrtypeIpv6 = 0x04;

    }
}
