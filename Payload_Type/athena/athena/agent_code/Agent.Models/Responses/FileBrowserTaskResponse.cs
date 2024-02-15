using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class FileBrowserTaskResponse : TaskResponse
    {
        public FileBrowser file_browser { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, FileBrowserResponseJsonContext.Default.FileBrowserTaskResponse);
        }
    }
    [JsonSerializable(typeof(FileBrowserTaskResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(Dictionary<string,string>))]
    public partial class FileBrowserResponseJsonContext : JsonSerializerContext
    {
    }

    public class FileBrowser
    {
        public string host { get; set; }
        public bool is_file { get; set; }
        public Dictionary<string, string> permissions { get; set; }
        public string name { get; set; }
        public string? parent_path { get; set; }
        public bool success { get; set; }
        public UInt64 access_time { get; set; }
        public UInt64 modify_time { get; set; }
        public long size { get; set; }
        public bool update_deleted { get; set; }
        public List<FileBrowserFile> files { get; set; }

    }
    public class FileBrowserFile
    {
        public bool is_file { get; set; }
        public Dictionary<string, string> permissions { get; set; }
        public string name { get; set; }
        public UInt64 access_time { get; set; }
        public UInt64 modify_time { get; set; }
        public long size { get; set; }
    }
}
