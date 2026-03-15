using System.Net;
using System.Text.Json;
using System.Text;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "wget";
        private IDataBroker messageManager { get; set; }
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (args.ContainsKey("url"))
                {
                    DebugLog.Log($"{Name} requesting {args["url"]} [{job.task.id}]");
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
                                    messageManager.AddTaskResponse(new TaskResponse()
                                    {
                                        completed = true,
                                        user_output = Get(req),
                                        task_id = job.task.id
                                    });
                                    break;
                                case "post":
                                    if (!String.IsNullOrEmpty(args["body"]))
                                    {
                                        messageManager.AddTaskResponse(new TaskResponse()
                                        {
                                            completed = true,
                                            user_output = Post(req, args["body"].ToString()),
                                            task_id = job.task.id
                                        });
                                    }
                                    else
                                    {
                                        messageManager.AddTaskResponse(new TaskResponse()
                                        {
                                            completed = true,
                                            user_output = Post(req, ""),
                                            task_id = job.task.id
                                        });
                                    }
                                    break;
                                default:
                                    messageManager.AddTaskResponse(new TaskResponse()
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
                            messageManager.AddTaskResponse(new TaskResponse()
                            {
                                completed = true,
                                user_output = Get(req),
                                task_id = job.task.id
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        DebugLog.Log($"{Name} request error: {e.Message} [{job.task.id}]");
                        messageManager.Write(e.ToString(), job.task.id, true, "error");
                        return;
                    }

                }
                else
                {
                    DebugLog.Log($"{Name} no URL specified [{job.task.id}]");
                    messageManager.AddTaskResponse(new TaskResponse()
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
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
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