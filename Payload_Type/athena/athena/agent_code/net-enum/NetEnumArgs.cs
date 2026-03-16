namespace net_enum
{
    public class NetEnumArgs
    {
        public string action { get; set; } = "ping";
        // ping/traceroute params
        public string host { get; set; } = "";
        public int timeout { get; set; } = 1000;
        public int count { get; set; } = 4;
        public int max_ttl { get; set; } = 30;
        // arp params
        public string cidr { get; set; } = "";
        // test-port params
        public string hosts { get; set; } = "";
        public string ports { get; set; } = "";
        public string targetlist { get; set; } = "";
    }
}
