using System;
using System.Net;
namespace Athena
{
    public static class Plugin
    {

        public static string Execute(string[] args)
        {
            return Dns.GetHostName();
        }
    }
}
