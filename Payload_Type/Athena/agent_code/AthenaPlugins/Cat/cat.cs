using System.Collections.Generic;
using System.IO;
namespace Athena
{
    public static class Plugin
    {
        public static string Execute(Dictionary<string, object> args)
        {
            if (args.ContainsKey("path"))
            {
                return File.ReadAllText((string)args["path"]);
            }
            else
            {
                return "A path needs to be specified";
            }
        }
    }
}

