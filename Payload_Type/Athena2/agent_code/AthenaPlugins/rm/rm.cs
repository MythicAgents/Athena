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
                File.Delete(args[0]);
                return "Deleted File: " + args[0];
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
