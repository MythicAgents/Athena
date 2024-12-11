using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Utilities;
namespace execute_module
{
    public class ExecModuleArgs
    {
        public string file { get; set; } = string.Empty;
        public string name { get; set; }
        public string entrypoint { get; set; }
        public string arguments { get; set; } = string.Empty;

        public List<string> GetArgs()
        {
            return Misc.SplitCommandLine(arguments).ToList();
        }
    }
}
