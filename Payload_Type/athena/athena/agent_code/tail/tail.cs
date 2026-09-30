using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Linq;
using System.Text.Json;
using tail;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "tail";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
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
                return;
            }

            try
            {
                using var reader = File.OpenText(args.path);
                IReadOnlyList<string> text = TailReader.ReadLastLines(reader, args.lines);

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
        private async Task Watch(TailArgs args, string taskId, CancellationToken token)
        {
            var path = TailPath.Resolve(args.path);
            using var fileStream = new FileStream(path.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream);

            var existingLines = TailReader.ReadLastLines(streamReader, args.lines);
            messageManager.Write(string.Join(Environment.NewLine, existingLines), taskId, false);

            await using var pump = new TailChangePump(_ =>
            {
                while (streamReader.ReadLine() is { } line)
                {
                    messageManager.WriteLine(line, taskId, false);
                }
                return Task.CompletedTask;
            });
            using var watcher = new FileSystemWatcher(path.Directory, path.FileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            FileSystemEventHandler changed = (_, _) => pump.Signal();
            watcher.Changed += changed;
            watcher.EnableRaisingEvents = true;
            try
            {
                await pump.RunAsync(token).ConfigureAwait(false);
            }
            finally
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= changed;
            }
        }
    }

}
