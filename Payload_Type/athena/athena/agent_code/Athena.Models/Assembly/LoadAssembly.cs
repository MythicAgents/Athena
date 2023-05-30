using Athena.Models.Mythic.Checkin;
using System.Text.Json.Serialization;

namespace Athena.Models.Assembly
{
    public class LoadAssembly
    {
        public string asm { get; set; }
        public string target { get; set; }
    }
    [JsonSerializable(typeof(LoadAssembly))]
    public partial class LoadAssemblyJsonContext : JsonSerializerContext
    {
    }
}
