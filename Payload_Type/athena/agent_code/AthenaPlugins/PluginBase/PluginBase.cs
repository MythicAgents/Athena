using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginBase
{
    public abstract class AthenaPlugin : IPlugin
    {
        public abstract string Name { get; }
        public abstract void Execute(Dictionary<string, object> args);
    }
}
