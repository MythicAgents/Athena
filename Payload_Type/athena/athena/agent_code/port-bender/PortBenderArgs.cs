namespace port_bender
{
    public class PortBenderArgs
    {
        public int port { get; set; }
        public string destination { get; set; } = string.Empty;

        public bool Validate() => port is >= 1 and <= 65535 && !string.IsNullOrWhiteSpace(destination);
    }
}
