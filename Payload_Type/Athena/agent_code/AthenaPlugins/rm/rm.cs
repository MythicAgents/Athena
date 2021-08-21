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
                if (args.ContainsKey("file"))
                {
                    File.Delete((string)args["file"]);
                    return "Deleted File: " + (string)args["file"];
                }
                else
                {
                    return "Please specify a file to delete!";
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
