using System;
using System.IO;
namespace Athena
{
    public static class Plugin
    {

        public static string Execute(string[] args)
        {
            try
            {
                Directory.SetCurrentDirectory(args[0]);
                return "Changed current directory to " + args[0];
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
