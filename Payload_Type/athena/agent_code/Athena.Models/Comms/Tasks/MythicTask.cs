namespace Athena.Models.Mythic.Tasks
{
    [Serializable]
    public class MythicTask
    {
        public string command { get; set; }
        public string parameters { get; set; }
        public string id { get; set; }
        //public string timestamp { get; set; }
        public int token { get; set; }
    }

}
