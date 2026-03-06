using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Linq;
using System.Text.Json;
using tail;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "tail";
        private IDataBroker messageManager { get; set; }
        private ICredentialProvider tokenManager { get; set; }
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            TailArgs args = JsonSerializer.Deserialize<TailArgs>(job.task.parameters);
            if(args is null){
                return;
            }

            if (args.watch)
            {
                await Watch(args, job.task.id, job.cancellationtokensource.Token);
            }

            try
            {
                List<string> text = File.ReadLines(args.path).Reverse().Take(args.lines).ToList();
                text.Reverse();

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = string.Join(Environment.NewLine, text),
                    task_id = job.task.id,
                });
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
        private async Task Watch(TailArgs args, string task_id, CancellationToken token)
        {
            using (FileStream fileStream = new FileStream(args.path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader streamReader = new StreamReader(fileStream))
            {
                var fileContents = string.Join(Environment.NewLine, streamReader.ReadToEnd().Split(Environment.NewLine).Reverse().Take(args.lines).Reverse().ToList());
                
                // Display existing content of the file
                this.messageManager.Write(fileContents, task_id, false);

                // Set up a FileSystemWatcher to monitor the file for changes
                using (FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(args.path), Path.GetFileName(args.path)))
                {
                    watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;

                    watcher.Changed += (sender, e) =>
                    {

                        // Read and display new lines
                        while (!streamReader.EndOfStream)
                        {
                            this.messageManager.WriteLine(streamReader.ReadLine().Replace(Environment.NewLine, ""), task_id, false);
                        }
                    };

                    // Start watching
                    watcher.EnableRaisingEvents = true;

                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(1000);
                    }
                }
            }
        }
    }

}
