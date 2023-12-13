using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.IO;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Mkdir : IPlugin
    {
        public string Name => "mkdir";

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
            try
            {
                if (args.ContainsKey("path"))
                {
                    DirectoryInfo dir = Directory.CreateDirectory((args["path"]).Replace("\"", ""));

                    TaskResponseHandler.AddResponse(new ResponseResult
                    {
                        completed = true,
                        user_output = "Created directory " + dir.FullName,
                        task_id = args["task-id"],
                    });
                }
                else
                {
                    TaskResponseHandler.AddResponse(new ResponseResult
                    {
                        completed = true,
                        process_response = new Dictionary<string, string> { { "message", "0x2A" } },
                        task_id = args["task-id"],
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

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
