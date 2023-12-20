using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace config
{
    internal class ConfigUpdateArgs
    {

        internal int sleep { get; set; } = -1;
        internal int jitter { get; set; } = -1;
        internal DateTime killdate { get; set; } = DateTime.MinValue;
    }
}
