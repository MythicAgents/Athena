using System.ComponentModel.DataAnnotations;
namespace Workflow.Models
{
    public static class SmbMessageType
    {
        public const string ConnectionReady = "connection_ready";
        public const string Chunked = "chunked_message";
        public const string MessageComplete = "message_complete";
        public const string Error = "error";
    }

    [Serializable]
    public class SmbMessage
    {
        public string guid { get; set; }
        public string message_type { get; set; }
        public string delegate_message { get; set; }
        public string agent_guid { get; set; }
        public bool final { get; set; }
        public int sequence { get; set; }
    }
}
