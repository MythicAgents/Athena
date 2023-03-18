using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Commands.Models
{
    public interface IPlugin
    {
        //Execute the plugin
        public string Name { get; }
        public void Execute(Dictionary<string, string> args);
    }
}
