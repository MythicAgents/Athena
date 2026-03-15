using System.Reflection;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Providers;

namespace Workflow.Tests
{
    public class PluginLoader
    {
        private readonly PluginContext context;

        public PluginLoader(IDataBroker messageManager)
        {
            context = new PluginContext(
                messageManager,
                new TestServiceConfig(),
                new TestLogger(),
                new TestCredentialProvider(),
                new TestSpawner(),
                new ScriptEngine()
            );
        }

        public IModule? LoadPluginFromDisk(string moduleName)
        {
            return GetPlugin(moduleName);
        }

        private IModule? GetPlugin(string moduleName)
        {
            try
            {
                Assembly asm = Assembly.Load(
                    AssemblyNames.ForModule(moduleName));

                foreach (Type t in asm.GetTypes())
                {
                    if (typeof(IModule).IsAssignableFrom(t))
                    {
                        return (IModule)Activator.CreateInstance(t, context);
                    }
                }
                return null;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
