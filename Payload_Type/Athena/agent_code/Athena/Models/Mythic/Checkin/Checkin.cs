namespace Athena.Models.Mythic.Checkin
{
    public class Checkin
    {
        public string action { get; set; }
        public string ip { get; set; }
        public string os { get; set; }
        public string user { get; set; }
        public string host { get; set; }
        public string pid { get; set; }
        public string uuid { get; set; }
        public string architecture { get; set; }
        public string domain { get; set; }
        public int integrity_level { get; set; }
        public string external_ip { get; set; } = "";
        public string encryption_key { get; set; } = "";
        public string decryption_key { get; set; } = "";
    }

}
