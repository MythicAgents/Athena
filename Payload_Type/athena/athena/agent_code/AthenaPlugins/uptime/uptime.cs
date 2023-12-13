using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Uptime : IPlugin
    {
        public string Name => "uptime";

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
            var Uptime64 = TimeSpan.FromMilliseconds(Environment.TickCount64);
            string UptimeD = Uptime64.Days.ToString();
            string UptimeH = Uptime64.Hours.ToString();
            string UptimeM = Uptime64.Minutes.ToString();
            string UptimeS = Uptime64.Seconds.ToString();

            TaskResponseHandler.AddResponse(new ResponseResult
            {
                completed = true,
                user_output = Environment.NewLine + UptimeD + " Days " + UptimeH + " Hours " + UptimeM + " Mins " + UptimeS + " Seconds ",
                task_id = args["task-id"],
            });
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}