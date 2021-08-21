using System;
using System.Collections.Generic;
using CoreSploit.Enumeration;

namespace Athena
{
    public static class Plugin
    {
        //We can pass dictionaries to functions. I just need to figure out how I want to do it on the agent side.
        public static string Execute(Dictionary<string,object> args)
        {
            //Domain.DomainSearcher ds = new Domain.DomainSearcher();
            string output = "";

            output += "Int Value: " + args["int"];
            output += "\r\nString Value: " + args["string"];
            output += "\r\nBool Value: " + args["bool"];

            return output;
        }
    }
}
