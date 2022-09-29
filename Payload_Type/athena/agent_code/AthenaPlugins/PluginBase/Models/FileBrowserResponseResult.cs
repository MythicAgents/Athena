namespace Athena.Plugins
{
    public class FileBrowserResponseResult : ResponseResult
    {
        public FileBrowser file_browser { get; set; }
    }

    public class FileBrowser
    {
        public string host { get; set; }
        public bool is_file { get; set; }
        public Dictionary<string, string> permissions { get; set; }
        public string name { get; set; }
        public string? parent_path { get; set; }
        public bool success { get; set; }
        public string access_time { get; set; }
        public string modify_time { get; set; }
        public long size { get; set; }
        public bool update_deleted { get; set; }
        public List<FileBrowserFile> files { get; set; }

    }
    public class FileBrowserFile
    {
        public bool is_file { get; set; }
        public Dictionary<string, string> permissions { get; set; }
        public string name { get; set; }
        public string access_time { get; set; }
        public string modify_time { get; set; }
        public long size { get; set; }
    }
}
