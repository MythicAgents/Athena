using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Commands
{
    public class Enumeration
    {
        //Should I Just make EVERY command loadable?

        public string GetFileContent(string filename)
        {
            //cat
            return File.ReadAllText(filename);
        }
        public string MakeDirectory(string directoryname)
        {

            return "";
        }
        //LDAP related functions will go in here
        //I should probably limit what's ACTUALLY in here and make them reflective DLL's that get loaded and executed instead
    }
}
