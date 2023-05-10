using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.IO;
using Athena.Commands;

namespace Plugins
{
    public class Pwd : AthenaPlugin
    {
        public override string Name => "pwd";
        public override void Execute(Dictionary<string, string> args)
        {
            TaskResponseHandler.AddResponse(new ResponseResult
            {
                completed = true,
                user_output = Directory.GetCurrentDirectory(),
                task_id = args["task-id"],
            });
        }
    }
}
