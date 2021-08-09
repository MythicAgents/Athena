using System.IO;
using System;

namespace Athena
{
    public static class Plugin
    {

        public static string Execute(string[] args)
        {
            try
            {
                File.Move(args[0], args[1]);
                return String.Format("Copied {0} tp {1}", args[0], args[1]);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
