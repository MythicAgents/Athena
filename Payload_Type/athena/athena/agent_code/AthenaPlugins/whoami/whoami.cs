using System;
using System.Collections.Generic;
using Athena.Models;
using Athena.Commands.Models;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class WhoAmI : IPlugin
    {
        public string Name => "whoami";

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
            TaskResponseHandler.AddResponse(new ResponseResult()
            {
                task_id = args["task-id"],
                user_output = $"{Environment.UserDomainName}\\{Environment.UserName}",
                completed = true
            });
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
