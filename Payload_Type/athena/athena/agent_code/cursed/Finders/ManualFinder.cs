using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class ManualFinder : IFinder
    {
        private string cmdline;
        public ManualFinder(string cmdline)
        {
            this.cmdline = cmdline;
        }
        public string FindPath()
        {
            return cmdline;
        }
    }
}
