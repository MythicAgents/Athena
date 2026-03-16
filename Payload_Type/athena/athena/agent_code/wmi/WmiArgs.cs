namespace wmi
{
    public class WmiArgs
    {
        public string action { get; set; } = "query";
        public string query { get; set; } = "";
        public string ns { get; set; } = @"root\cimv2";
        // wmi-exec fields
        public string host { get; set; } = "";
        public string command { get; set; } = "";
        public string username { get; set; } = "";
        public string password { get; set; } = "";
    }
}
