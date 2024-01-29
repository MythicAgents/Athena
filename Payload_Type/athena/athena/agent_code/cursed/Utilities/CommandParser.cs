using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    internal class CommandParser
    {
        internal static string GetHelpText()
        {
            return """
    Commands:
        cursed [chrome|edge]
            Enumerates a spawned electron process via the local debugging port for extensions with permissions suitable for CursedChrome. 
                If a payload is specified it will use that, if not, it will use the built-in payload with the target setting 

        set [config] [value]
            Set's a configuration value. For cursed commands
                set debug_port 2020 //Set's the port to be used for the electron debug port
                set payload <payload> //Sets the payload to be used
                set target ws[s]://target:port //Sets the target for the default payload, this parameter is ignored if the payload has been manually set
                set cmdline "--user-data-dir=C:\\Users\\checkymander\\"
                set parent <pid>

        get [target|payload|extensions|debug-port]
            Get's the value of the configuration parameter and prints it to output
""";
        }
    }
}
