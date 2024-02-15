using System.Net;
using System.Text.Json;
using System.Text;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "wget";
        private IMessageManager messageManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (args.ContainsKey("url"))
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(args["url"].ToString());

                    if (args.ContainsKey("cookies"))
                    {
                        if (!String.IsNullOrEmpty(args["cookies"]) && args["cookies"].ToString().StartsWith('{'))
                        {
                            Dictionary<string, string> cookies = JsonSerializer.Deserialize<Dictionary<string, string>>(args["cookies"]);
                            CookieContainer cc = new CookieContainer();
                            foreach (var kvp in cookies)
                            {
                                Cookie c = new Cookie(kvp.Key, kvp.Value);
                                cc.Add(c);
                            }
                            req.CookieContainer = cc;
                        }
                    }

                    if (args.ContainsKey("headers"))
                    {
                        if (!String.IsNullOrEmpty(args["headers"]) && args["headers"].ToString().StartsWith('{'))
                        {
                            Dictionary<string, string> headers = JsonSerializer.Deserialize<Dictionary<string, string>>(args["headers"]);

                            foreach (var kvp in headers)
                            {
                                if (kvp.Key.ToLower() == "host")
                                {
                                    req.Host = kvp.Value;
                                }
                                else
                                {
                                    req.Headers.Add(kvp.Key, kvp.Value);
                                }
                            }
                        }
                    }

                    try
                    {
                        if (args.ContainsKey("method"))
                        {
                            switch (args["method"].ToString().ToLower())
                            {
                                case "get":
                                    await messageManager.AddResponse(new TaskResponse()
                                    {
                                        completed = true,
                                        user_output = Get(req),
                                        task_id = job.task.id
                                    });
                                    break;
                                case "post":
                                    if (!String.IsNullOrEmpty(args["body"]))
                                    {
                                        await messageManager.AddResponse(new TaskResponse()
                                        {
                                            completed = true,
                                            user_output = Post(req, args["body"].ToString()),
                                            task_id = job.task.id
                                        });
                                    }
                                    else
                                    {
                                        await messageManager.AddResponse(new TaskResponse()
                                        {
                                            completed = true,
                                            user_output = Post(req, ""),
                                            task_id = job.task.id
                                        });
                                    }
                                    break;
                                default:
                                    await messageManager.AddResponse(new TaskResponse()
                                    {
                                        completed = true,
                                        user_output = Get(req),
                                        task_id = job.task.id
                                    });
                                    break;
                            }
                        }
                        else
                        {
                            await messageManager.AddResponse(new TaskResponse()
                            {
                                completed = true,
                                user_output = Get(req),
                                task_id = job.task.id
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        messageManager.Write(e.ToString(), job.task.id, true, "error");
                        return;
                    }

                }
                else
                {
                    await messageManager.AddResponse(new TaskResponse()
                    {
                        completed = true,
                        task_id = job.task.id,
                        user_output = "A URL needs to be specified.",
                        status = "error"
                    });
                }
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
        private string Get(HttpWebRequest req)
        {
            try
            {
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        private string Post(HttpWebRequest req, string data)
        {
            try
            {
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);

                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                req.ContentLength = dataBytes.Length;
                //req.ContentType = contentType;
                //req.Method = method;

                using (Stream requestBody = req.GetRequestStream())
                {
                    requestBody.Write(dataBytes, 0, dataBytes.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}