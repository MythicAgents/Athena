using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workflow.Models
{
    public class FileDeletedTaskResponse : TaskResponse
    {
        public List<DeletedFile> removed_files { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, FileDeletedResponseJsonContext.Default.FileDeletedTaskResponse);
        }
    }
    [JsonSerializable(typeof(FileDeletedTaskResponse))]
    public partial class FileDeletedResponseJsonContext : JsonSerializerContext
    {
    }
    public class DeletedFile
    {
        public string host { get; set; }
        public string path { get; set; }
    }
}
