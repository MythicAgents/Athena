using System;
using System.Collections.Generic;
using PluginBase;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponseError Execute(Dictionary<string, object> args)
        {
            var dr = new PluginResponseError()
            {
                result = "An error occurred.",
                errorMessage = "yinz fucked up",
                success = false
            };

            return dr;

        }
    }

}
