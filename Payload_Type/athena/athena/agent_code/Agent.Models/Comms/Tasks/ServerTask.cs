namespace Agent.Models
{
    [Serializable]
    public class ServerTask
    {
        public string command { get; set; }
        public string parameters { get; set; }
        public string id { get; set; }
        public int token { get; set; }
    }

}
