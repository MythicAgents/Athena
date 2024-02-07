using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace rm
{
    public class RmArgs
    {
        public string path { get; set; }
        public string file { get; set; }
        public string host { get; set; }

        public bool Validate(out string message)
        {
            message = string.Empty;

            //If we didn't get a path, then return an error
            if (string.IsNullOrEmpty(path))
            {
                message = "Missing path parameter";
                return false;
            }

            //If we get to this point we either have a full path, or we're using the file browser and have all three
            //If we have a file combine it with the existing path
            if (!string.IsNullOrEmpty(file))
            {
                this.path = Path.Combine(this.path, this.file);
            }

            //If we have a host, append it to the beginning of the path
            if (!string.IsNullOrEmpty(host))
            {
                if (!host.Equals(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase))
                {
                    host = "\\\\" + host;
                    this.path = Path.Combine(this.host, this.path);
                }
            }

            if (!File.Exists(this.path) && !Directory.Exists(this.path))
            {
                message = $"Path doesn't exist: {path}";
                return false;
            }

            return true;
        }
    }
}
