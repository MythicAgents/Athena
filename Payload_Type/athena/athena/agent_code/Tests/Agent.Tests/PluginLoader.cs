using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
//using Agent.Config;
using Agent.Managers;
using Agent.Interfaces;
using Agent.Models;
using System.ComponentModel;

namespace Agent.Tests
{
    public class PluginLoader
    {
        public IMessageManager messageManager { get; set; } = new TestMessageManager();
        public IAgentConfig agentConfig { get; set; } = new TestAgentConfig();
        public ILogger logger { get; set; } = new TestLogger();
        public ITokenManager tokenManager { get; set; } = new TestTokenManager();
        public ISpawner spawner { get; set; } = new TestSpawner();
        public IPythonManager pyManager { get; set; } = new PythonManager();
        public IPlugin? LoadPluginFromDisk(string pluginName)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "LocalDebugHttp", "net8.0", $"{pluginName}.dll");
            byte[] buf = File.ReadAllBytes(path);
            Assembly asm = Assembly.Load(buf);

            return ParseAssemblyForPlugin(asm, this.messageManager, this.agentConfig, this.logger, this.tokenManager, this.spawner, this.pyManager);
        }
        public PluginLoader(IMessageManager messageManager)
        {
            this.messageManager = messageManager;
        }

        private static IPlugin ParseAssemblyForPlugin(Assembly asm, IMessageManager messageManager, IAgentConfig agentConfig, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pyManager)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(t))
                {
                    IPlugin plug = (IPlugin)Activator.CreateInstance(t, messageManager, agentConfig, logger, tokenManager, spawner, pyManager);
                    return plug;
                }
            }
            return null;
        }
    }
}
