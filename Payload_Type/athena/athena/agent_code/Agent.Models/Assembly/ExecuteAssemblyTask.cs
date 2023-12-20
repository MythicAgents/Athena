using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class ExecuteAssemblyTask
    {
        public string asm { get; set; }
        public string arguments { get; set; }
    }
    [JsonSerializable(typeof(ExecuteAssemblyTask))]
    public partial class ExecuteAssemblyTaskJsonContext : JsonSerializerContext
    {
    }

}
