namespace fileutils
{
    public class FileUtilsArgs
    {
        public string action { get; set; } = "head";
        public string path { get; set; } = "";
        public string path2 { get; set; } = "";
        public int lines { get; set; } = 10;
        public string mode { get; set; } = "";
        public string owner { get; set; } = "";
        public string group { get; set; } = "";
        public string link_type { get; set; } = "symbolic";
    }
}
