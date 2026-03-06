using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflow.Channels.Smb
{
    public class MessageReceivedArgs : EventArgs
    {
        public string message { get; set; }
        public MessageReceivedArgs(string message)
        {
            this.message = message;
        }
    }
}
