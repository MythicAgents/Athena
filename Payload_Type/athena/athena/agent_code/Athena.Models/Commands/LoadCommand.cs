using Athena.Models.Athena.Assembly;
using System.Text.Json.Serialization;

namespace Athena.Models.Athena.Commands
{
    public class LoadCommand
    {
        public string command { get; set; }
        public string asm { get; set; }
    }
    [JsonSerializable(typeof(LoadCommand))]
    [JsonSerializable(typeof(string))]
    public partial class LoadCommandJsonContext : JsonSerializerContext
    {
    }
}

