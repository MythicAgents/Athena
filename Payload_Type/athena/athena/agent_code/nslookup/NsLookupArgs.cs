using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nslookup
{
    public class NsLookupArgs
    {
        public string targetlist { get; set; } = string.Empty;
        public string hosts { get; set; } = string.Empty;

        public bool Validate(out string message)
        {
            message = string.Empty;
            if(string.IsNullOrEmpty(targetlist) && string.IsNullOrEmpty(hosts))
            {
                message = "Target list or hosts must be specified";
                return false;
            }
            return true;
        }
    }
}
