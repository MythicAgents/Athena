namespace Athena.Commands.Model
{
    public class Socks5Options
    {

        public string ProxyHost { get; }
        public int ProxyPort { get; }
        public string DestinationHost { get; }
        public int DestinationPort { get; }
        public AuthType? Auth { get; }
        public (string Username, string Password) Credentials { get; }

        public Socks5Options(string proxyHost, int proxyPort, string destHost, int destPort)
        {
            ProxyHost = proxyHost;
            ProxyPort = proxyPort;
            DestinationHost = destHost;
            DestinationPort = destPort;
            Auth = AuthType.None;
        }

        public Socks5Options(string proxyHost, string destHost, int destPort) : this(proxyHost, 1080, destHost, destPort) { }

        public Socks5Options(string proxyHost, int proxyPort, string destHost, int destPort, string username,
            string password) : this(proxyHost, proxyPort, destHost, destPort)
        {
            Auth = AuthType.UsernamePassword;
            Credentials = (username, password);
        }

        public Socks5Options(string proxyHost, string destHost, int destPort, string username, string password) :
            this(proxyHost, 1080, destHost, destPort, username, password)
        { }

    }

    public enum AuthType
    {
        None,
        UsernamePassword
    }
}
