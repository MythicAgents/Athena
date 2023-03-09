using System;
using System.Collections.Generic;
using Athena.Models;
using Athena.Commands.Models;
using Athena.Commands;

namespace Plugins
{
    public class WhoAmI : AthenaPlugin
    {
        public override string Name => "whoami";
        public override void Execute(Dictionary<string, string> args)
        {
            TaskResponseHandler.AddResponse(new ResponseResult()
            {
                task_id = args["task-id"],
                user_output = $"{Environment.UserDomainName}\\{Environment.UserName}",
                completed = true
            });
        }
    }
}
