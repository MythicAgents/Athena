
namespace Athena.Profiles.Discord.Models
{
    public class Attachment
    {
        public string id { get; set; }
        public string filename { get; set; }
        public string? description { get; set; }
        public string? content_type { get; set; }
        public int size { get; set; }
        public string url { get; set; }
        public string proxy_url { get; set; }
        public int? height { get; set; }
        public int? width { get; set; }
        public bool? ephemeral { get; set; }
    }
}
