using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Config
{
    public interface IConfig
    {
        public static string uuid { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public IProfile currentConfig { get; set; }
        //public IForwarder forwarder { get; set; }
    }
}
