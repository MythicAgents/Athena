using System;
using System.Collections.Generic;

namespace Athena
{
    public static class Plugin
    {

        public static string Execute(Dictionary<string, object> args)
        {
            return Environment.UserDomainName + "\\" + Environment.UserName;
        }
    }
}
