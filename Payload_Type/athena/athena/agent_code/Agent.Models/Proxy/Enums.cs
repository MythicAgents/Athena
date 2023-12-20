namespace Agent.Models
{
    public enum AddressType : byte
    {
        IPv4 = 0x01,
        DomainName = 0x03,
        IPv6 = 0x04
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
