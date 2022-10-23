using Athena.Models;
using Athena.Plugins;
using System;
using System.Collections.Generic;

namespace Plugins
{
    public class Uptime : AthenaPlugin
    {
        public override string Name => "uptime";
        public override void Execute(Dictionary<string, string> args)
        {
            var Uptime64 = TimeSpan.FromMilliseconds(Environment.TickCount64);
            string UptimeD = Uptime64.Days.ToString();
            string UptimeH = Uptime64.Hours.ToString();
            string UptimeM = Uptime64.Minutes.ToString();
            string UptimeS = Uptime64.Seconds.ToString();

            PluginHandler.AddResponse(new ResponseResult
            {
                completed = "true",
                user_output = Environment.NewLine + UptimeD + " Days " + UptimeH + " Hours " + UptimeM + " Mins " + UptimeS + " Seconds ",
                task_id = args["task-id"],
            });
        }
    }
}