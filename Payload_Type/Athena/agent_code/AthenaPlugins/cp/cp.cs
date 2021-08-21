using System;
using System.Collections.Generic;
using System.IO;
namespace Athena
{
    public static class Plugin
    {

        public static string Execute(Dictionary<string, object> args)
        {
            try
            {
                if(args.ContainsKey("source") && args.ContainsKey("destination"))
                {
                    File.Copy((string)args["source"], (string)args["destination"]);
                    return String.Format("Copied {0} tp {1}", (string)args["source"], (string)args["destination"]);
                }
                else
                {
                    return "Please specify both a source and destination for the file!";
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
