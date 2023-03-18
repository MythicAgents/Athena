
namespace Athena.Profiles.Discord.Models
{
    public class Author
    {
        public string id { get; set; }
        public string username { get; set; }
        public object avatar { get; set; }
        public object avatar_decoration { get; set; }
        public string discriminator { get; set; }
        public int public_flags { get; set; }
        public bool? bot { get; set; }
    }
}
