using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class FileDeletedTaskResponse : TaskResponse
    {
        public List<DeletedFile> removed_files { get; set; } = new();

        public override string ToJson()
        {
            return JsonSerializer.Serialize(this, FileDeletedTaskResponseJsonContext.Default.FileDeletedTaskResponse);
        }
    }

    [JsonSerializable(typeof(FileDeletedTaskResponse))]
    [JsonSerializable(typeof(DeletedFile))]
    public partial class FileDeletedTaskResponseJsonContext : JsonSerializerContext
    {
    }

    public class DeletedFile
    {
        public string host { get; set; }
        public string path { get; set; }
    }
}
