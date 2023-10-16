
namespace Athena.Profiles.Discord.Models
{
    public class MythicMessageWrapper
    {
        public string message { get; set; } = string.Empty;
        public string sender_id { get; set; } //Who sent the message
        public bool to_server { get; set; }
        public int id { get; set; }
        public bool final { get; set; }
    }
}
