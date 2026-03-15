using Workflow.Providers;
using Workflow.Tests.TestClasses;
using Workflow.Tests.TestInterfaces;

namespace Workflow.Tests.AssemblyTests
{
    [TestClass]
    public class ComponentProviderTests
    {
        IDataBroker _messageManager = new TestDataBroker();
        PluginContext _context;

        public ComponentProviderTests()
        {
            _context = new PluginContext(
                _messageManager,
                new TestServiceConfig(),
                new TestLogger(),
                new TestCredentialProvider(),
                new TestSpawner(),
                null);
        }

        private string GetPluginDllPath(string pluginName)
        {
            var cwd = Directory.GetCurrentDirectory();
            var configDir = new DirectoryInfo(cwd).Parent?.Name ?? "Debug";
            return Path.Combine(
                cwd, "..", "..", "..", "..", "..",
                pluginName, "bin", configDir, "net10.0",
                $"{pluginName}.dll");
        }

        [TestMethod]
        public void LoadAssemblyAsync_Success()
        {
            var path = GetPluginDllPath("cat");
            var assemblyManager = new ComponentProvider(_context);

            byte[] assemblyBytes = File.ReadAllBytes(path);
            bool result = assemblyManager.LoadAssemblyAsync("123", assemblyBytes);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void LoadAssemblyAsync_InvalidBytes()
        {
            var assemblyManager = new ComponentProvider(_context);

            byte[] garbageBytes = new byte[] { 0x00, 0x01, 0x02, 0xFF };
            bool result = assemblyManager.LoadAssemblyAsync("456", garbageBytes);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void LoadModuleAsync_Success()
        {
            var assemblyManager = new ComponentProvider(_context);

            var path = GetPluginDllPath("cat");
            Assert.IsTrue(File.Exists(path));

            var buf = File.ReadAllBytes(path);
            bool result = assemblyManager.LoadModuleAsync("123", "cat", buf);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void LoadModuleAsync_DuplicateReturnsFalse()
        {
            var assemblyManager = new ComponentProvider(_context);

            var path = GetPluginDllPath("cat");
            var buf = File.ReadAllBytes(path);

            bool first = assemblyManager.LoadModuleAsync("123", "cat", buf);
            Assert.IsTrue(first);

            bool second = assemblyManager.LoadModuleAsync("456", "cat", buf);
            Assert.IsFalse(second);
        }

        [TestMethod]
        public void TryGetModuleReflection_Success()
        {
            var assemblyManager = new ComponentProvider(_context);

            string moduleName = "ds";
            var path = GetPluginDllPath(moduleName);
            Assert.IsTrue(File.Exists(path));

            var buf = File.ReadAllBytes(path);
            assemblyManager.LoadModuleAsync("123", moduleName, buf);

            bool result = assemblyManager.TryGetModule(
                moduleName, out IModule? actualPlugin);

            Assert.IsTrue(result);
            Assert.IsNotNull(actualPlugin);
            Assert.AreEqual(moduleName, actualPlugin.Name);
        }

        [TestMethod]
        public void TryGetModule_Failure()
        {
            var assemblyManager = new ComponentProvider(_context);

            bool result = assemblyManager.TryGetModule(
                "NonExistentPlugin", out IModule? actualPlugin);

            Assert.IsFalse(result);
            Assert.IsNull(actualPlugin);
        }

        [TestMethod]
        public void LoadModuleAsync_ThenExecute()
        {
            var assemblyManager = new ComponentProvider(_context);

            var path = GetPluginDllPath("cat");
            var buf = File.ReadAllBytes(path);
            assemblyManager.LoadModuleAsync("123", "cat", buf);

            bool found = assemblyManager.TryGetModule(
                "cat", out IModule? plugin);

            Assert.IsTrue(found);
            Assert.IsNotNull(plugin);
            Assert.AreEqual("cat", plugin.Name);
        }

        [TestMethod]
        public void LoadModuleAsync_InvalidBytes()
        {
            var assemblyManager = new ComponentProvider(_context);

            byte[] garbageBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            bool result = assemblyManager.LoadModuleAsync(
                "789", "garbage", garbageBytes);

            Assert.IsFalse(result);
        }
    }
}
