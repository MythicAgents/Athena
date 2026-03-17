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
        private AssemblyLoadContext loadContext;
        private readonly PluginContext context;
        public ComponentProvider(PluginContext context) {
            this.context = context;
            this.loadContext = new AssemblyLoadContext(Misc.RandomString(10));
            this.loadContext.Resolving += (ctx, name) =>
            {
                try
                {
                    return AssemblyLoadContext.Default.LoadFromAssemblyName(name);
                }
                catch
                {
                    return null;
                }
            };
        }
        
        private bool TryLoadModule(
            string name, out IModule? plugOut)
        {
            DebugLog.Log(
                "TryLoadModule: scanning assemblies for "
                + name);
            plugOut = null;

            foreach (var asm
                in AppDomain.CurrentDomain.GetAssemblies())
            {
                ParseAssemblyForModule(asm);
            }

            if (loadedModules.TryGetValue(name, out plugOut))
                return true;

            try
            {
                var asm = Assembly.Load(
                    new AssemblyName(name));
                if (ParseAssemblyForModule(asm))
                    return loadedModules.TryGetValue(
                        name, out plugOut);
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    "TryLoadModule: fallback load failed for "
                    + name + ": " + e.Message);
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
                context.MessageManager.AddTaskResponse(new TaskResponse
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
                context.MessageManager.AddTaskResponse(new TaskResponse
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
                if (this.loadedModules.ContainsKey(moduleName))
                {
                    this.context.MessageManager.AddTaskResponse(new LoadTaskResponse
                    {
                        completed = true,
                        user_output = "Module already loaded.",
                        task_id = task_id,
                        status = "error"
                    });
                    return false;
                }

                var loadedAssembly = this.loadContext.LoadFromStream(new MemoryStream(buf));

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
                this.context.MessageManager.AddTaskResponse(new LoadTaskResponse
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
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var le in ex.LoaderExceptions)
                {
                    if (le is null) continue;
                    var detail = le is TypeLoadException tle
                        ? $"{tle.GetType().Name}: {tle.Message} (TypeName={tle.TypeName})"
                        : le.ToString();
                    DebugLog.Log($"ParseAssemblyForModule: loader exception: {detail}");
                }
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }

            foreach (Type t in types)
            {
                if (typeof(IModule).IsAssignableFrom(t)
                    && !t.IsAbstract && !t.IsInterface)
                {
                    IModule? plug = Activator.CreateInstance(t, context) as IModule;
                    if (plug is null) continue;
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
