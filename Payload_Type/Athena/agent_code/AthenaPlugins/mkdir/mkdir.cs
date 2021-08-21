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
                    DirectoryInfo dir = Directory.CreateDirectory((string)args["path"]);
                    return "Created directory " + dir.FullName;
                }
                else
                {
                    return "Please specify a directory to create!";
                }

            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
