 using System;
using System.Collections.Generic;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            var Uptime64 = TimeSpan.FromMilliseconds(Environment.TickCount64);
            string UptimeD = Uptime64.Days.ToString();
            string UptimeH = Uptime64.Hours.ToString();
            string UptimeM = Uptime64.Minutes.ToString();
            string UptimeS = Uptime64.Seconds.ToString();


            return new PluginResponse()
            {
                success = true,
                output = Environment.NewLine + UptimeD + " Days " + UptimeH + " Hours " + UptimeM + " Mins " + UptimeS + " Seconds "
            };
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}