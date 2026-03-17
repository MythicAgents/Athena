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
            : this(messageManager, new TestServiceConfig())
        {
        }

        public PluginLoader(IDataBroker messageManager, IServiceConfig config)
        {
            context = new PluginContext(
                messageManager,
                config,
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
            foreach (var asm
                in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types
                        .Where(t => t is not null).ToArray()!;
                }

                foreach (var t in types)
                {
                    if (typeof(IModule).IsAssignableFrom(t)
                        && !t.IsAbstract && !t.IsInterface)
                    {
                        var plug = (IModule?)
                            Activator.CreateInstance(t, context);
                        if (plug?.Name == moduleName)
                            return plug;
                    }
                }
            }
            return null;
        }
    }
}
