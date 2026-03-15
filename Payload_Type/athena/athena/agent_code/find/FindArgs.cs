namespace find
{
    public class FindArgs
    {
        public string action { get; set; } = "find";
        public string path { get; set; } = ".";
        public string pattern { get; set; } = "*";
        public string content_pattern { get; set; } = "";
        public int max_depth { get; set; } = 10;
        public long min_size { get; set; } = -1;
        public long max_size { get; set; } = -1;
        public string permissions { get; set; } = "";
        public string newer_than { get; set; } = "";
        public string older_than { get; set; } = "";
    }
}
