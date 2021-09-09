using System.Linq;

namespace Athena.Commands.Model.Socks
{
    public class ConnectRequest
    {
        byte version { get; set; }
        byte command { get; set; }
        byte rsv = 0x00;
        byte[] dstaddr { get; set; }
        byte[] dstport { get; set; }

        public byte[] ToByte()
        {
            byte[] arr = { this.version, this.command, this.rsv };

            arr.Concat(this.dstaddr);
            arr.Concat(this.dstport);

            return arr;
        }
    }
}
