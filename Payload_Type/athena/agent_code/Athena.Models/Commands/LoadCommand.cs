using Athena.Models.Athena.Assembly;
using System.Text.Json.Serialization;

namespace Athena.Models.Athena.Commands
{
    public class LoadCommand
    {
        public string command;
        public string asm;
    }
    [JsonSerializable(typeof(LoadCommand))]
    public partial class LoadCommandJsonContext : JsonSerializerContext
    {
    }
}

