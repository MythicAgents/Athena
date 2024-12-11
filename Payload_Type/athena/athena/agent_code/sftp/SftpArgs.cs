using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sftp
{
    public class SftpArgs
    {
        public string hostname { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string keypath { get; set; } 
    }
}
