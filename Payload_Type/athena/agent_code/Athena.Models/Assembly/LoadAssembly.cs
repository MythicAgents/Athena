using Athena.Models.Mythic.Checkin;
using System.Text.Json.Serialization;

namespace Athena.Models.Athena.Assembly
{
    public class LoadAssembly
    {
        public string asm;
        public string target;
    }
    [JsonSerializable(typeof(LoadAssembly))]
    public partial class LoadAssemblyJsonContext : JsonSerializerContext
    {
    }
}
