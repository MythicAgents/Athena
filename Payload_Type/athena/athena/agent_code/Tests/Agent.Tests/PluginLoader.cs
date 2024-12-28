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
using static IronPython.Modules._ast;

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
            return GetPlugin(pluginName);
        }

        private IPlugin? GetPlugin(string pluginName)
        {
            try
            {
                Assembly _tasksAsm = Assembly.Load($"{pluginName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
                foreach (Type t in _tasksAsm.GetTypes())
                {
                    if (typeof(IPlugin).IsAssignableFrom(t))
                    {
                        IPlugin plug = (IPlugin)Activator.CreateInstance(t, messageManager, agentConfig, logger, tokenManager, spawner, pyManager);
                        return plug;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        public PluginLoader(IMessageManager messageManager)
        {
            this.messageManager = messageManager;
        }
    }
}
