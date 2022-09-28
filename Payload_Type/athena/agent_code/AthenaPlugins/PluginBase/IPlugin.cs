using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginBase
{
    public interface IPlugin
    {
        //Execute the plugin
        public void Execute(Dictionary<string, object> args);
    }
}
