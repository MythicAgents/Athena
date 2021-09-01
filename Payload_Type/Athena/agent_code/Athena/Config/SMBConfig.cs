using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class SmbServer
    {
        public string namedpipe { get; set; }
        public List<string> messages { get; set; }
        public bool Server { get; set; }
        public bool Client { get; set; }

        public SmbServer()
        {
            this.Server = bool.Parse("%SMBSERVER%") ? this.Server : false;
            this.Client = bool.Parse("%SMBCLIENT%") ? this.Client : false;

            if (this.Server)
            {
                //start server
            }
            if (this.Client)
            {

            }
        }
    }
}
