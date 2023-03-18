
namespace Athena.Profiles.Discord.Models
{
    public class GetServerDetails
    {
        public string id { get; set; }
        public int type { get; set; }
        public string name { get; set; }
        public int position { get; set; }
        public int flags { get; set; }
        public string parent_id { get; set; }
        public string guild_id { get; set; }
        public List<object> permission_overwrites { get; set; }
        public string last_message_id { get; set; }
        public object topic { get; set; }
        public int? rate_limit_per_user { get; set; }
        public bool? nsfw { get; set; }
        public int? bitrate { get; set; }
        public int? user_limit { get; set; }
        public object rtc_region { get; set; }
    }
}
