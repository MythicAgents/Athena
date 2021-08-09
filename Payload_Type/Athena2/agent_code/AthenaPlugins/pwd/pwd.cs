using System;
using System.IO;

namespace Athena
{
    public static class Plugin
    {

        public static string Execute(string[] args)
        {
            return Directory.GetCurrentDirectory();
        }
    }
}
