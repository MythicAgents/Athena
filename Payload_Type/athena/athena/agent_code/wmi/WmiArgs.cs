namespace wmi
{
    public class WmiArgs
    {
        public string action { get; set; } = "query";
        public string query { get; set; } = "";
        public string ns { get; set; } = @"root\cimv2";
    }
}
