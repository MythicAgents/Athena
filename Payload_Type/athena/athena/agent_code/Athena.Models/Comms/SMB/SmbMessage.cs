using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Comms.SMB
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
