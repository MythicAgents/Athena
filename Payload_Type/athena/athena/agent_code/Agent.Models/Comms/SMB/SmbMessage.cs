namespace Agent.Models
{
    [Serializable]
    public class SmbMessage
    {
        public string guid { get; set; }
        public string message_type { get; set; }
        public string delegate_message { get; set; }
        public bool final { get; set; }
    }
}
