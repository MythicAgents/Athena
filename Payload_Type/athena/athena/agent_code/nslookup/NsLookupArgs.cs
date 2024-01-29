using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nslookup
{
    public class NsLookupArgs
    {
        public string targetlist { get; set; } = String.Empty;
        public string hosts { get; set; } = String.Empty;

        public bool Validate(out string message)
        {
            message = String.Empty;
            if(String.IsNullOrEmpty(targetlist) && String.IsNullOrEmpty(hosts))
            {
                message = "Target list or hosts must be specified";
                return false;
            }
            return true;
        }
    }
}
