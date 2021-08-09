using System;

namespace Athena
{
    public static class Plugin
    {

        public static string Execute(string[] args)
        {
            return Environment.UserDomainName + "\\" + Environment.UserName;
        }
    }
}
