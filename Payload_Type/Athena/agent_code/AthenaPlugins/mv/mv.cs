using System.IO;
using System;
using System.Collections.Generic;

namespace Athena
{
    public static class Plugin
    {

        public static string Execute(Dictionary<string, object> args)
        {
            if (args.ContainsKey("source") && args.ContainsKey("destination"))
            {
                File.Move((string)args["source"], (string)args["destination"]);
                return String.Format("Moved {0} tp {1}", (string)args["source"], (string)args["destination"]);
            }
            else
            {
                return "Please specify both a source and destination for the file!";
            }
        }
    }
}
