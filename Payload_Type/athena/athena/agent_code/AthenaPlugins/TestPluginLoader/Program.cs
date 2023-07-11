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
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("task-id", "1");
            await TestReg(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
            Console.WriteLine("Finished.");
            Console.ReadKey();
        }

        static async Task TestCoff(Dictionary<string, string> parameters)
        {
            parameters.Add("asm", Misc.Base64Encode(File.ReadAllBytes(@"C:\Users\scott\Downloads\whoami.x64.o")));
            parameters.Add("functionName", "go");
            parameters.Add("arguments","");
            parameters.Add("timeout", "30");

            new Coff().Execute(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }

        static async Task TestShellcodeInject(Dictionary<string, string> parameters)
        {
            string json = "{\"blockDlls\":false , \"output\":false}";


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
            IPlugin plugin = new InjectShellcode();

            plugin.Execute(parameters);

            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }
        static async Task TestRm(Dictionary<string, string> parameters)
        {
            parameters.Add("host", "mydesktop");
            parameters.Add("path", @"C$\Users\scott\Downloads\");
            parameters.Add("file", "test.txt");
            new Rm().Execute(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }

        static async Task TestLs(Dictionary<string, string> parameters)
        {
            //string json = """{"path": "Users\\scott\\source\\repos\\Athena\\athena", "host": "DESKTOP-GRJNOH2"}""";
            string json = """{"path": "C:", "host": "DESKTOP-GRJNOH2"}""";
            IPlugin plug = new Ls();

            plug.Execute(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());

        }

        static async Task TestPs(Dictionary<string, string> parameters)
        {
            new Ps().Execute(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }

        static async Task TestKeylogger(Dictionary<string, string> parameters)
        {
            new Keylogger().Execute(parameters);
        }

        static async Task TestReg(Dictionary<string, string> parameters)
        {
            parameters.Add("keypath", @"HKEY_CURRENT_USER\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
            parameters.Add("action", @"query");
            parameters.Add("hostname", "127.0.0.1");
            new Reg().Execute(parameters);
        }
    }
}
