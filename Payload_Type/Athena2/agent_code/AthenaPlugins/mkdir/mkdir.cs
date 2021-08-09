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
                DirectoryInfo dir = Directory.CreateDirectory(args[0]);
                return "Created directory " + dir.FullName;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
