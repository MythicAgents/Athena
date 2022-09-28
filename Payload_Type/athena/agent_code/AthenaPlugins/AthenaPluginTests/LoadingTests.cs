using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Loader;
using System.IO;
using PluginBase;
using Plugin;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace AthenaPluginTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestLoad()
        {
            ConcurrentDictionary<string, IPlugin> plugins = new ConcurrentDictionary<string, IPlugin>();
            AssemblyLoadContext alc = new AssemblyLoadContext("test");
            byte[] b = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\Payload_Type\athena\agent_code\AthenaPlugins\newami\bin\Debug\net6.0\newami.dll");
            byte[] b2 = File.ReadAllBytes(@"C:\Users\scott\source\repos\Athena\Payload_Type\athena\agent_code\AthenaPlugins\whoami\bin\Debug\net6.0\whoami.dll");

            Assembly asm = alc.LoadFromStream(new MemoryStream(b));
            Assembly asm2 = alc.LoadFromStream(new MemoryStream(b2));
            Type t = asm.GetType($"Plugins.Plugin");
            Type t2 = asm2.GetType($"Plugins.Plugin");
            IPlugin plugin = (IPlugin)Activator.CreateInstance(t);
            IPlugin plugin2 = (IPlugin)Activator.CreateInstance(t2);

            plugins.GetOrAdd("newami", plugin);
            plugins.GetOrAdd("whoami", plugin2);

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "task-id", "1" }
            };

            plugin.Execute(parameters);
            plugin2.Execute(parameters);
        }
    }
}
