namespace enum_windows
{
    public class EnumWindowsArgs
    {
        public string action { get; set; } = "get-localgroup";
        // get-localgroup params
        public string group { get; set; } = "";
        public string hostname { get; set; } = "";
        // get-sessions + get-shares params
        public string hosts { get; set; } = "";
        public string targetlist { get; set; } = "";
    }
}
