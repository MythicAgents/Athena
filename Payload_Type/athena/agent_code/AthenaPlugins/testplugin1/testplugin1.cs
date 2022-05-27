using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using PluginBase;
using Newtonsoft.Json;
namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse2 Execute(Dictionary<string, object> args)
        {

            var ur = new PluginResponse2()
            {
                result = "test",
                success = true,

            };

            return ur;

        }
    }
}
