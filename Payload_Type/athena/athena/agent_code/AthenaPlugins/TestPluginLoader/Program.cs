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
using System.Threading.Tasks;
using System.Reflection.Metadata.Ecma335;

namespace TestPluginLoader
{
    class Program
    {
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");
        static async Task Main(string[] args)
        {
            await TestRm();
            Console.WriteLine("Finished.");
            Console.ReadKey();
        }

        static async Task TestCoff()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("asm", Misc.Base64Encode(File.ReadAllBytes(@"C:\Users\scott\Downloads\whoami.x64.o")));
            parameters.Add("functionName", "go");
            parameters.Add("arguments","");
            parameters.Add("timeout", "30");

            parameters.Add("task-id", "1");
            new Coff().Execute(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }

        static async Task TestShellcodeInject()
        {
            string json = "{\"blockDlls\":false , \"output\":false}";

            Dictionary<string, string> parameters = Misc.ConvertJsonStringToDict(json);

            //Dictionary<string, string> parameters = new Dictionary<string, string>();
            //parameters.Add("asm", Misc.Base64Encode(File.ReadAllBytes(@"C:\Users\scott\Downloads\Seatbelt\Seatbelt.bin")));
            System.Diagnostics.Process[] p = System.Diagnostics.Process.GetProcessesByName("explorer");
            parameters.Add("asm", Misc.Base64Encode(File.ReadAllBytes(@"C:\Users\scott\Downloads\donut\loader.bin")));
            parameters.Add("arguments", "");
            parameters.Add("processName", "notepad.exe");
            parameters.Add("parent", p.FirstOrDefault().Id.ToString());
            parameters.Add("blockdlls", "true");
            //parameters.Add("functionName", "go");
            //parameters.Add("timeout", "60");
            parameters.Add("task-id", "1");
            IPlugin plugin = new ShellcodeInject();

            plugin.Execute(parameters);

            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }
        static async Task TestRm()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("host", "mydesktop");
            parameters.Add("path", @"C$\Users\scott\Downloads\");
            parameters.Add("file", "test.txt");
            parameters.Add("task-id", "1");
            new Rm().Execute(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }
    }
}
