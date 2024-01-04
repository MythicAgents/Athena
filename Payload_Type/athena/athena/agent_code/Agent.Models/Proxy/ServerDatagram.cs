using Agent.Utilities;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    //Can also be used for RPortFwd
    [Serializable]
    public class ServerDatagram
    {
        public bool exit { get; set; }
        public int server_id { get; set; }
        public string data { get; set; }
        [JsonIgnore]
        public byte[] bdata { get; set; }
        [JsonIgnore]
        public DatagramSource source { get; set; }

        public ServerDatagram(int server_id, byte[] bdata, bool exit)
        {
            this.exit = exit;
            this.server_id = server_id;
            this.bdata = bdata;

            if(bdata is not null)
            {
                this.data = Misc.Base64Encode(bdata);
            }
        }
    }
}
