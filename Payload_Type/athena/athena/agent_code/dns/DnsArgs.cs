namespace dns
{
    public class DnsArgs
    {
        public string action { get; set; } = "resolve";
        public string hostname { get; set; } = "";
        public string record_type { get; set; } = "A";
        public string hosts { get; set; } = "";
        public string targetlist { get; set; } = "";
    }
}
