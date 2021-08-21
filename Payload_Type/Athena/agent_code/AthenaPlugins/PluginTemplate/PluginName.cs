using System;
using System.Collections.Generic;

namespace Athena
{
    public static class Plugin
    {

        public static string Execute(Dictionary<string, object> args)
        {
            if (args.ContainsKey("myparameter"))
            {
                return "Found the parameter!";
            }

            if (args.ContainsKey("message"))
            {
                return (string)args["message"];
            }

            return "End!";
        }
    }
}
