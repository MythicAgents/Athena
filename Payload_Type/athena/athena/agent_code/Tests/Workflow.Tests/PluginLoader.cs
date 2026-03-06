using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
//using Workflow.Config;
using Workflow.Providers;
using Workflow.Contracts;
using Workflow.Models;
using System.ComponentModel;
using static IronPython.Modules._ast;

namespace Workflow.Tests
{
    public class PluginLoader
    {
        public IDataBroker messageManager { get; set; } = new TestDataBroker();
        public IServiceConfig agentConfig { get; set; } = new TestServiceConfig();
        public ILogger logger { get; set; } = new TestLogger();
        public ICredentialProvider tokenManager { get; set; } = new TestCredentialProvider();
        public IRuntimeExecutor spawner { get; set; } = new TestSpawner();
        public IScriptEngine pyManager { get; set; } = new ScriptEngine();
        public IModule? LoadPluginFromDisk(string moduleName)
        {
            return GetPlugin(moduleName);
        }

        private IModule? GetPlugin(string moduleName)
        {
            try
            {
                Assembly _tasksAsm = Assembly.Load($"{moduleName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
                foreach (Type t in _tasksAsm.GetTypes())
                {
                    if (typeof(IModule).IsAssignableFrom(t))
                    {
                        IModule plug = (IModule)Activator.CreateInstance(t, messageManager, agentConfig, logger, tokenManager, spawner, pyManager);
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
        public PluginLoader(IDataBroker messageManager)
        {
            this.messageManager = messageManager;
        }
    }
}
