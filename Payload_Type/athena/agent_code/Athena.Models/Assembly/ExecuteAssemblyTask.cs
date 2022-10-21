using Athena.Models.Athena.Commands;
using System.Text.Json.Serialization;

namespace Athena.Models.Athena.Assembly
{
    public class ExecuteAssemblyTask
    {
        public string asm;
        public string arguments;
    }
    [JsonSerializable(typeof(ExecuteAssemblyTask))]
    public partial class ExecuteAssemblyTaskJsonContext : JsonSerializerContext
    {
    }

}
