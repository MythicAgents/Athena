using System.Net;
using System.Net.Sockets;

namespace port_bender
{
    public static class EndpointParser
    {
        public static async Task<IPEndPoint> ResolveAsync(
            string destination,
            Func<string, Task<IPAddress[]>>? resolver = null)
        {
            (string host, int port) = Parse(destination);
            if (IPAddress.TryParse(host, out IPAddress? address))
                return new IPEndPoint(address, port);

            resolver ??= hostName => Dns.GetHostAddressesAsync(hostName);
            IPAddress[] addresses = await resolver(host).ConfigureAwait(false);
            if (addresses.Length == 0)
                throw new SocketException((int)SocketError.HostNotFound);
            return new IPEndPoint(addresses[0], port);
        }

        private static (string Host, int Port) Parse(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
                throw new FormatException("Destination must be in host:port or [IPv6]:port form.");

            string host;
            string portText;
            if (destination[0] == '[')
            {
                int closingBracket = destination.IndexOf(']');
                if (closingBracket <= 1 || closingBracket + 1 >= destination.Length || destination[closingBracket + 1] != ':')
                    throw new FormatException("Bracketed IPv6 destination must be in [address]:port form.");
                host = destination[1..closingBracket];
                portText = destination[(closingBracket + 2)..];
            }
            else
            {
                int separator = destination.LastIndexOf(':');
                if (separator <= 0 || separator != destination.IndexOf(':'))
                    throw new FormatException("Destination must be in host:port form; IPv6 addresses require brackets.");
                host = destination[..separator];
                portText = destination[(separator + 1)..];
            }

            if (!int.TryParse(portText, out int port) || port is < 1 or > 65535)
                throw new FormatException("Destination port must be between 1 and 65535.");
            return (host, port);
        }
    }
}
