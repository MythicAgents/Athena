using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Linq;
using System.Text;
using Athena.Commands.Models;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Plugins;
using Athena.Commands;

namespace TestPluginLoader
{
    class Program
    {
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");
        static void Main(string[] args)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("asm", "AAAAAAAAAAAAAA=");
            parameters.Add("output", false.ToString());
            //parameters.Add("task_id", "1");
            IPlugin plugin = new ShellcodeExec();

            plugin.Execute(parameters);
            Console.WriteLine(TaskResponseHandler.GetTaskResponsesAsync().Result.FirstOrDefault());
        }
    }
}
