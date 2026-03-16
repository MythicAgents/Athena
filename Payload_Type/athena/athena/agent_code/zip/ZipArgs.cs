namespace Workflow
{
    public class ZipArgs
    {
        public string action { get; set; } = "compress";
        public string source { get; set; } = "";
        public string destination { get; set; } = "";
        public string path { get; set; } = "";
        public bool write { get; set; } = false;
        public bool force { get; set; } = false;
    }
}
