using System.IO;
namespace Athena
{
    public static class Plugin
    {
        public static string Execute(string[] args)
        {
            return File.ReadAllText(args[0]);
        }
    }
}

