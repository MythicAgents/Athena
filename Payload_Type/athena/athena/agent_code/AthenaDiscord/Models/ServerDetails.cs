
namespace Athena.Profiles.Discord.Models
{
    public class ServerDetails
    {
        public string id { get; set; }
        public int type { get; set; }
        public string content { get; set; }
        public string channel_id { get; set; }
        public Author author { get; set; }
        public List<Attachment> attachments { get; set; }
        public List<object> embeds { get; set; }
        public List<object> mentions { get; set; }
        public List<object> mention_roles { get; set; }
        public bool pinned { get; set; }
        public bool mention_everyone { get; set; }
        public bool tts { get; set; }
        public DateTime timestamp { get; set; }
        public object edited_timestamp { get; set; }
        public int flags { get; set; }
        public List<object> components { get; set; }
        public string webhook_id { get; set; }
    }
}
