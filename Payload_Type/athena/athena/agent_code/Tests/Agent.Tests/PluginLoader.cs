using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
//using Agent.Config;
using Agent.Managers;
using Agent.Interfaces;

namespace Agent.Tests
{
    internal class PluginLoader
    {
        public static IPlugin? LoadPluginFromDisk(string pluginName, IMessageManager messageManager, IAgentConfig agentConfig, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "Debug", "net7.0", $"{pluginName}.dll");
            byte[] buf = File.ReadAllBytes(path);
            Assembly asm = Assembly.Load(buf);

            return ParseAssemblyForPlugin(asm, messageManager, agentConfig, logger, tokenManager, spawner);
        }

        private static IPlugin ParseAssemblyForPlugin(Assembly asm, IMessageManager messageManager, IAgentConfig agentConfig, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(t))
                {
                    IPlugin plug = (IPlugin)Activator.CreateInstance(t, messageManager, agentConfig, logger, tokenManager, spawner);
                    return plug;
                }
            }
            return null;
        }
    }
}
