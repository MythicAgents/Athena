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
using Athena.Utilities;

namespace TestPluginLoader
{
    class Program
    {
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");
        static void Main(string[] args)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("asm", Misc.Base64Encode(File.ReadAllBytes(@"C:\Users\scott\OneDrive\driversigs.x64.o")));
            parameters.Add("arguments", "");
            parameters.Add("functionName", "go");
            parameters.Add("timeout", "60");
            parameters.Add("task-id", "1");
            IPlugin plugin = new Coff();

            plugin.Execute(parameters);
            Console.WriteLine(TaskResponseHandler.GetTaskResponsesAsync().Result.FirstOrDefault());
        }
    }
}
