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
            List<string> potentialDllPaths = new List<string>()
            {
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "Debug", "net8.0", $"{pluginName}.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "Release", "net8.0", $"{pluginName}.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "LocalDebugDiscord", "net8.0", $"{pluginName}.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "LocalDebugHttp", "net8.0", $"{pluginName}.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "LocalDebugWebsocket", "net8.0", $"{pluginName}.dll"),
            };


            foreach (string path in potentialDllPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }
    }
}
