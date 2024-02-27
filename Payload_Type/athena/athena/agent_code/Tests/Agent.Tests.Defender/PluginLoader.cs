using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Agent.Tests.Defender
{
    internal class PluginLoader
    {
        public static string GetPluginPath(string pluginName)
        {
            var debug_path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "Debug", "net7.0", $"{pluginName}.dll");
            var release_path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "Release", "net7.0", $"{pluginName}.dll");

            if (Path.Exists(release_path))
            {
                return release_path;
            }

            if (Path.Exists(debug_path))
            {
                return debug_path;
            }

            return string.Empty;
        }
    }
}
