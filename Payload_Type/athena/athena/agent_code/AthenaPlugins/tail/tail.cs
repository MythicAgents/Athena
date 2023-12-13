using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Tail : IPlugin
    {
        public string Name => "tail";

        public bool Interactive => false;

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }

        public void Start(Dictionary<string, string> args)
        {
            if (!args.ContainsKey("path") || string.IsNullOrEmpty(args["path"].ToString()))
            {
                TaskResponseHandler.Write("Please specify a path!", args["task-id"], true, "error");
                return;
            }
            string path = args["path"].ToString();
            int lines = 5;
            if (args.ContainsKey("lines"))
            {
                try
                {
                    lines = int.Parse(args["lines"]);
                }
                catch
                {
                    lines = 5;
                }
            }
            try
            {
                List<string> text = File.ReadLines(path).Reverse().Take(lines).ToList();
                text.Reverse();

                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    completed = true,
                    user_output = string.Join(Environment.NewLine, text),
                    task_id = args["task-id"],
                });
            }
            catch (Exception e)
            {
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }

}
