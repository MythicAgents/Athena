using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Workflow.Tests.Validation
{
    internal class PluginLoader
    {
        public static string GetPluginPath(string moduleName)
        {
            List<string> potentialDllPaths = new List<string>()
            {
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", moduleName, "bin", "Debug", "net10.0", $"{moduleName}.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", moduleName, "bin", "Release", "net10.0", $"{moduleName}.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", moduleName, "bin", "LocalDebugDiscord", "net10.0", $"{moduleName}.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", moduleName, "bin", "LocalDebugHttp", "net10.0", $"{moduleName}.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", moduleName, "bin", "LocalDebugWebsocket", "net10.0", $"{moduleName}.dll"),
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
