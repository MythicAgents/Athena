using Athena.Utilities;
using System.Text.Json.Serialization;

namespace Athena.Models.Proxy
{
    //Can also be used for RPortFwd
    [Serializable]
    public class MythicDatagram
    {
        public bool exit { get; set; }
        public int server_id { get; set; }
        public string data { get; set; }
        [JsonIgnore]
        public byte[] bdata { get; set; }

        public MythicDatagram(int server_id, byte[] bdata, bool exit)
        {
            this.exit = exit;
            this.server_id = server_id;
            this.bdata = bdata;
            this.data = null;
        }

        public void PrepareMessage()
        {
            if(this.bdata.Length > 0)
            {
                this.data = Misc.Base64Encode(this.bdata);
            }
        }
        public void Clear()
        {
            if(this.bdata.Length > 0)
            {
                this.bdata = new byte[0];
                this.data = null;
            }
        }
    }
}
