using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Mythic.Model.Misc
{
    public class Message
    {
        public Guid uuid { get; set; }
        public string data { get; set; }

        public Message()
        {

        }

        public Message(Guid uuid, string data)
        {
            this.uuid = uuid;
            this.data = data;
        }
    }
}
