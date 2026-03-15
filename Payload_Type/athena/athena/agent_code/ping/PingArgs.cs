namespace ping
{
    public class PingArgs
    {
        public string action { get; set; } = "ping";
        public string host { get; set; } = "";
        public int timeout { get; set; } = 1000;
        public int count { get; set; } = 4;
        public int max_ttl { get; set; } = 30;
    }
}
