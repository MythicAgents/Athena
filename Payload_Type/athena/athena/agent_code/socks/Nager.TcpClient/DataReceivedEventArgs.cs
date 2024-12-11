using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nager.TcpClient
{
    public class DataReceivedEventArgs
    {
        public int server_id { get; set; }
        public byte[] bytes { get; set; }
    }
}
