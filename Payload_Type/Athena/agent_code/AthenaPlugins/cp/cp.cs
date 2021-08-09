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
                File.Copy(args[0], args[1]);
                return String.Format("Copied {0} tp {1}",args[0],args[1]);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
