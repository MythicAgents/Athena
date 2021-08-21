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
                if (args.ContainsKey("path"))
                {
                    Directory.SetCurrentDirectory((string)args["path"]);
                    return "Changed current directory to " + (string)args["path"];
                }
                else
                {
                    return "Please specify a path!";
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
