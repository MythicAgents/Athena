namespace Agent.Utilities;

public readonly record struct SshEndpoint(string Host, int Port)
{
    public const int DefaultPort = 22;

    public static SshEndpoint Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Host is required.", nameof(value));

        string host;
        string? portText = null;
        if (value[0] == '[')
        {
            int close = value.IndexOf(']');
            if (close <= 1) throw new ArgumentException("Invalid bracketed host.", nameof(value));
            host = value[1..close];
            string suffix = value[(close + 1)..];
            if (suffix.Length != 0)
            {
                if (suffix[0] != ':' || suffix.Length == 1) throw new ArgumentException("Invalid bracketed host suffix.", nameof(value));
                portText = suffix[1..];
            }
        }
        else
        {
            int colon = value.IndexOf(':');
            if (colon < 0) host = value;
            else
            {
                if (colon == 0 || colon != value.LastIndexOf(':') || colon == value.Length - 1)
                    throw new ArgumentException("IPv6 addresses must be bracketed and ports must be present.", nameof(value));
                host = value[..colon];
                portText = value[(colon + 1)..];
            }
        }

        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(value));
        int port = DefaultPort;
        if (portText is not null && (!int.TryParse(portText, out port) || port is < 1 or > 65535))
            throw new ArgumentException("Port must be an integer from 1 through 65535.", nameof(value));
        return new SshEndpoint(host, port);
    }
}
