using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class DsArgs
    {
        public string action { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string domain { get; set; }
        public string ldapfilter { get; set; }
        public string objectcategory { get; set; }
        public string searchbase { get; set; }
        public string server { get; set; }
        public string properties { get; set; }
    }
}
