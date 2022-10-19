using Athena.Models.Mythic.Checkin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Athena.Models.Athena.Commands
{
    public class CreateToken
    {
        public string username { get; set; }
        public string password { get; set; }
        public string domain { get; set; }
        public string name { get; set; }
        public bool netOnly { get; set; }
    }
    [JsonSerializable(typeof(CreateToken))]
    public partial class CreateTokenJsonContext : JsonSerializerContext
    {
    }
}
