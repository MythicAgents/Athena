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
                if (args.ContainsKey("directory"))
                {
                    if(args.ContainsKey("force") && (string)args["force"] == "true")
                    {
                        Directory.Delete((string)args["directory"],true);
                        return "Deleted Directory and all sub files and folders in: " + (string)args["directory"];
                    }
                    else
                    {
                        Directory.Delete((string)args["directory"]);
                        return "Deleted Directory: " + (string)args["directory"];
                    }
                }
                else
                {
                    return "Please specify a directory to delete.";
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
