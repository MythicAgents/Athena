using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace http_server
{
    public class HttpServerArgs
    {
        public int port { get; set; } = 0;
        public string fileContents { get; set; } = String.Empty;
        public string fileName { get; set; } = String.Empty;
        public bool ssl { get; set; } = false;
        public string action { get; set; }

        public bool Validate()
        {
            switch (this.action.ToLower())
            {
                case "":
                    return false;
                case "start":
                    return this.port > 0 ? true : false;
                case "add-file":
                    return (!String.IsNullOrEmpty(this.fileContents) && !String.IsNullOrEmpty(this.fileName));
                default:
                    return false;
            }
        }
    }
}
