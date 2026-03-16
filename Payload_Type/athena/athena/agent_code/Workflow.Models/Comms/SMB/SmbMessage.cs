using System.ComponentModel.DataAnnotations;
namespace Workflow.Models
{
    public static class SmbMessageType
    {
        public const string Success = "success";
        public const string Chunked = "chunked_message";
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
