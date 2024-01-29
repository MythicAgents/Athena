using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using http_server;
using System.Net;
using Agent.Utilities;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "http-server";
        private IMessageManager messageManager { get; set; }
        private Dictionary<string, byte[]> availableFiles;
        private string start_task = String.Empty;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            HttpServerArgs args = JsonSerializer.Deserialize<HttpServerArgs>(job.task.parameters);
            if(!args.Validate()){
                await messageManager.WriteLine("Failed to validate params", job.task.id, true);
                return;
            }


            switch (args.action.ToLower())
            {
                case "start":
                    await Start(args.port, job.cancellationtokensource, job.task.id, false); 
                    break;
                case "host":
                    await AddFile(args.fileName, args.fileContents, job.task.id);
                    break;
                case "stop":
                    await Stop(job.task.id);
                    break;
                case "list":
                    StringBuilder sb = new StringBuilder();
                    foreach(var file in availableFiles)
                    {
                        sb.AppendLine(file.Key);
                    }
                    await messageManager.WriteLine(sb.ToString(), job.task.id, true);
                    break;
                default:
                    break;
            }
        }
        private async Task Start(int port, CancellationTokenSource cts, string task_id, bool ssl)
        {
            availableFiles = new Dictionary<string, byte[]>();
            using (HttpListener listener = new HttpListener())
            {
                listener.Prefixes.Add($"http://localhost:{port}/");
                if (ssl)
                {
                    listener.Prefixes.Add($"https://localhost:{port}/");
                }

                try
                {
                    listener.Start();
                }
                catch(Exception e)
                {
                    await messageManager.WriteLine(e.ToString(), task_id, false);
                    return;
                }

                this.start_task = task_id;
                await messageManager.WriteLine("Started on port " + port, task_id, false);

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        HttpListenerContext context = await listener.GetContextAsync();
                        _ = HandleRequestAsync(context);
                    }
                    catch (Exception ex)
                    {
                        await messageManager.Write(ex.ToString(), task_id, false, "error");
                    }
                }
            }
            await messageManager.WriteLine("Server exit.", task_id, true);
            start_task = String.Empty;
        }
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            await messageManager.WriteLine($"[{DateTime.Now}] Request for {context.Request.Url} from {context.Request.RemoteEndPoint}", start_task, false);
            string requestUrl = context.Request.Url.LocalPath.TrimStart('/');

            if (!availableFiles.ContainsKey(requestUrl))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            else
            {
                byte[] fileContent = availableFiles[requestUrl];

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/octet-stream";
                context.Response.ContentLength64 = fileContent.Length;

                await context.Response.OutputStream.WriteAsync(fileContent, 0, fileContent.Length);
            }

            context.Response.Close();
        }
        private async Task AddFile(string fileName, string fileContents, string task_id)
        {
            byte[] fileContent = Misc.Base64DecodeToByteArray(fileContents);
            availableFiles.Add(fileName, fileContent);
            await messageManager.Write($"{fileName} available at /{fileName}", task_id, false);
        }
        private async Task Stop(string task_id)
        {
            if (string.IsNullOrEmpty(start_task))
            {
                await messageManager.WriteLine("No task_id specified, is the server running?", task_id, true, "error");
                return;
            }

            if (!messageManager.TryGetJob(start_task, out var job))
            {
                await messageManager.WriteLine("Couldn't find job.", task_id, true, "error");
            }

            job.cancellationtokensource.Cancel();
            await messageManager.WriteLine("Server tasked to exit.", task_id, true);
        }
    }
}