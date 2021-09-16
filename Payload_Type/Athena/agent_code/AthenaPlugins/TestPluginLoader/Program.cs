using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Linq;
using System.Text;

namespace TestPluginLoader
{
    class Program
    {
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");
        static void Main(string[] args)
        {
            TestTail();
        }

        static void TestCat()
        {
            Console.WriteLine("Testing Cat:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\cat.dll");
            //loadedcommands.Add("Cat", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Assembly ass = loadcontext.LoadFromStream(new MemoryStream(asm));
            Type t = ass.GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { @"C:\Users\scott\Desktop\log.txt" } });
            Console.WriteLine(result);
        }
        static void TestDict()
        {
            Console.WriteLine("Testing Dict:");
            Dictionary<string, object> args = new Dictionary<string, object>();
            byte[] asm = File.ReadAllBytes(@"C:\Users\checkymander\source\repos\Athena\Payload_Type\Athena\agent_code\AthenaPlugins\GetDomainUsers\bin\Debug\netstandard2.0\GetDomainUsers.dll");
            //loadedcommands.Add("Cat", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Assembly ass = loadcontext.LoadFromStream(new MemoryStream(asm));
            Type t = ass.GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(Dictionary<string,object>)});
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("int", 100);
            dict.Add("string", "Hello World!");
            dict.Add("bool", true);
            var result = methodInfo.Invoke(null, new object[] { args });
            Console.WriteLine(result);
        }
        static void TestCD()
        {
            Console.WriteLine("Testing CD:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\cd.dll");
            loadedcommands.Add("CD", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["CD"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { @"C:\Users\scott\Desktop\" } });
            Console.WriteLine(result);
        }
        static void TestCP()
        {
            Console.WriteLine("Testing CP:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\cp.dll");
            loadedcommands.Add("CP", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["CP"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { @"C:\Users\scott\Desktop\log.txt", @"C:\Users\scott\Desktop\log2.txt" } });
            Console.WriteLine(result);
        }
        static void TestHostname()
        {
            Console.WriteLine("Testing Hostname:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\hostname.dll");
            loadedcommands.Add("Hostname", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["Hostname"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] {  } });
            Console.WriteLine(result);

        }
        static void TestIfConfig()
        {
            Dictionary<string, object> args = new Dictionary<string, object>();
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\Payload_Type\Athena\agent_code\AthenaPlugins\ifconfig\bin\Debug\net5.0\ifconfig.dll");
            loadedcommands.Add("ifconfig", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["ifconfig"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(Dictionary<string, object>) });
            var result = methodInfo.Invoke(null, new object[] { args });

            PluginResponse pr = new PluginResponse()
            {
                output = (string)result.GetType().GetProperty("output").GetValue(result),
                success = (bool)result.GetType().GetProperty("success").GetValue(result)
            };
            Console.WriteLine(pr.output);
        }
        static void Testls()
        {
            Console.WriteLine("Testing ls:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\ls.dll");
            loadedcommands.Add("ls", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["ls"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { @"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\"  } });
            Console.WriteLine(result);
        }
        static void Testmkdir()
        {
            Console.WriteLine("Testing mkdir:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\mkdir.dll");
            loadedcommands.Add("mkdir", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["mkdir"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { @"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin2\" } });
            Console.WriteLine(result);
        }
        static void Testmv()
        {
            Console.WriteLine("Testing mv:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\mv.dll");
            loadedcommands.Add("mv", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["mv"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { @"C:\Users\scott\Desktop\log2.txt", @"C:\Users\scott\Desktop\log3.txt" } });
            Console.WriteLine(result);
        }
        static void testps()
        {
            Console.WriteLine("Testing ps:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\ps.dll");
            loadedcommands.Add("ps", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["ps"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new Dictionary<string,object>() });
            Console.WriteLine(result);
        }
        static void testpwd()
        {
            Console.WriteLine("Testing pwd:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\pwd.dll");
            loadedcommands.Add("pwd", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["pwd"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { } });
            Console.WriteLine(result);
        }
        static void testrm()
        {
            Console.WriteLine("Testing rm:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\rm.dll");
            loadedcommands.Add("rm", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["rm"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { @"C:\Users\scott\Desktop\log3.txt" } });
            Console.WriteLine(result);
        }
        static void testrmdir()
        {
            Console.WriteLine("Testing rmdir:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\rmdir.dll");
            loadedcommands.Add("rmdir", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["rmdir"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { @"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin2\" } });
            Console.WriteLine(result);
        }
        static void TestTail()
        {
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add("lines", 100);
            args.Add("path", @"C:\Users\scott\Documents\huiion keyt.txt");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\Payload_Type\Athena\agent_code\AthenaPlugins\tail\bin\Debug\net5.0\tail.dll");
            loadedcommands.Add("tail", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["tail"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(Dictionary<string, object>) });
            var result = methodInfo.Invoke(null, new object[] { args });

            PluginResponse pr = new PluginResponse()
            {
                output = (string)result.GetType().GetProperty("output").GetValue(result),
                success = (bool)result.GetType().GetProperty("success").GetValue(result)
            };
            Console.WriteLine(pr.output);
        }
        static void testwhoami()
        {
            Console.WriteLine("Testing whoami:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\agent_code\AthenaPlugins\bin\whoami.dll");
            loadedcommands.Add("whoami", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Type t = loadedcommands["whoami"].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { new string[] { } });
            Console.WriteLine(result);
        }
        static void testenv()
        {
            Console.WriteLine("Testing Cat:");
            byte[] asm = File.ReadAllBytes(@"C:\Users\Dev\Desktop\Athena-main\Payload_Type\Athena\agent_code\AthenaPlugins\env\bin\Debug\net5.0\env.dll");
            //loadedcommands.Add("Cat", loadcontext.LoadFromStream(new MemoryStream(asm)));
            Assembly ass = loadcontext.LoadFromStream(new MemoryStream(asm));
            Type t = ass.GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(Dictionary<string, object>)});
            var result = methodInfo.Invoke(null, new object[] {new Dictionary <string,object>()});
            Console.WriteLine(result);
        }
    }
    public class PluginResponse
    {
        public bool success { get; set; }
        public string output { get; set; }
    }
    static class Misc
    {
        public static string[] SplitCommandLine(string commandLine)
        {
            bool inQuotes = false;

            return commandLine.Split(c =>
            {
                if (c == '\"')
                    inQuotes = !inQuotes;

                return !inQuotes && c == ' ';
            })
                              .Select(arg => arg.Trim().TrimMatchingQuotes('\"'))
                              .Where(arg => !string.IsNullOrEmpty(arg)).ToArray<string>();
        }

        public static IEnumerable<string> Split(this string str,
                                        Func<char, bool> controller)
        {
            int nextPiece = 0;

            for (int c = 0; c < str.Length; c++)
            {
                if (controller(str[c]))
                {
                    yield return str.Substring(nextPiece, c - nextPiece);
                    nextPiece = c + 1;
                }
            }

            yield return str.Substring(nextPiece);
        }
        public static string TrimMatchingQuotes(this string input, char quote)
        {
            if ((input.Length >= 2) &&
                (input[0] == quote) && (input[input.Length - 1] == quote))
                return input.Substring(1, input.Length - 2);

            return input;
        }
    }
}
