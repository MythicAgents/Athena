using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            if (!args.ContainsKey("path") || string.IsNullOrEmpty(args["path"].ToString()))
            {
                return new PluginResponse()
                {
                    success = false,
                    output = "You need to specify a path!"
                };
            }
            string path = args["path"].ToString();
            int lines = 5;
            if (args.ContainsKey("lines"))
            {
                try
                {
                    lines = (int)args["lines"];
                }
                catch
                {
                    lines = 5;
                }
            }
            try
            {
                List<string> text = File.ReadLines(path).Reverse().Take(lines).ToList();
                text.Reverse();

                return new PluginResponse()
                {
                    success = true,
                    output = string.Join(Environment.NewLine, text)
                };
            }
            catch (Exception e)
            {
                return new PluginResponse()
                {
                    success = false,
                    output = e.Message
                };
            }
        }
    }
    public class PluginResponse
    {
        public bool success { get; set; }
        public string output { get; set; }
    }
}
