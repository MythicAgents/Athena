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
                Directory.Delete(args[0]);
                //Directory.Delete(args[0],true) for recursive

                return "Deleted Directory: " + args[0];
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
