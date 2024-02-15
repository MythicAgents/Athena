using Agent.Managers;
using Agent.Tests.TestClasses;
using Agent.Tests.TestInterfaces;

namespace Agent.Tests.AssemblyTests
{
    [TestClass]
    public class AssemblyManagerTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        ISpawner _spawner = new TestSpawner();
        [TestMethod]
        public void LoadAssemblyAsync_Success()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "cat", "bin", "Debug", "net7.0", "cat.dll");

            // Arrange
            IAssemblyManager assemblyManager = new AssemblyManager(_messageManager, _logger, _config, _tokenManager, _spawner);
            string taskId = "123";
            byte[] assemblyBytes = File.ReadAllBytes(path);

            // Act
            bool result = assemblyManager.LoadAssemblyAsync(taskId, assemblyBytes);

            // Assert
            Assert.IsTrue(result);

        }

        [TestMethod]
        public void LoadPluginAsync_Success()
        {
            // Arrange
            IAssemblyManager assemblyManager = new AssemblyManager(_messageManager, _logger, _config, _tokenManager, _spawner);
            string taskId = "123";
            string pluginName = "SamplePlugin";
            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "cat", "bin", "Debug", "net7.0", "cat.dll");
            Assert.IsTrue(File.Exists(path));

            var buf = File.ReadAllBytes(path);

            // Act
            bool result = assemblyManager.LoadPluginAsync(taskId, pluginName, buf);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TryGetPluginReflection_Success()
        {
            //// Arrange
            IAssemblyManager assemblyManager = new AssemblyManager(_messageManager, _logger, _config, _tokenManager, _spawner);
            string pluginName = "ds";
            IPlugin expectedPlugin = PluginLoader.LoadPluginFromDisk(pluginName, _messageManager, _config, _logger, _tokenManager, _spawner) ;
            // Assuming you have a concrete implementation of IPlugin

            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", pluginName, "bin", "Debug", "net7.0", $"{pluginName}.dll");
            Assert.IsTrue(File.Exists(path));

            var buf = File.ReadAllBytes(path);


            // Act
            assemblyManager.LoadPluginAsync("123", pluginName, buf);
            bool result = assemblyManager.TryGetPlugin(pluginName, out IPlugin? actualPlugin);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNotNull(actualPlugin);
            Assert.AreSame(expectedPlugin.Name, actualPlugin.Name);
        }
        [TestMethod]
        public void TryGetPluginReference_Success()
        {
            //// Arrange
            //IAssemblyManager assemblyManager = new AssemblyManager(_messageManager, _logger, _config, _tokenManager);
            //string pluginName = "ds";
            //IPlugin expectedPlugin = new ds.Ds(_messageManager,_config, _logger, _tokenManager);
            //// Assuming you have a concrete implementation of IPlugin

            //// Act
            //assemblyManager.LoadPluginAsync("ds", pluginName, new byte[] { /* plugin bytes */ });
            //bool result = assemblyManager.TryGetPlugin(pluginName, out IPlugin? actualPlugin);

            //// Assert
            //Assert.IsTrue(result);
            //Assert.IsNotNull(actualPlugin);
            //Assert.AreSame(expectedPlugin.Name, actualPlugin.Name);
        }

        [TestMethod]
        public void TryGetPlugin_Failure()
        {
            // Arrange
            IAssemblyManager assemblyManager = new AssemblyManager(_messageManager, _logger, _config, _tokenManager, _spawner);
            string nonExistentPluginName = "NonExistentPlugin";

            // Act
            bool result = assemblyManager.TryGetPlugin(nonExistentPluginName, out IPlugin? actualPlugin);

            // Assert
            Assert.IsFalse(result);
            Assert.IsNull(actualPlugin);
        }

        [TestMethod]
        public void LoadingTasksCreatesPlugin_Success()
        {

        }
        [TestMethod]
        public void LoadingTasksCreatesPlugin_Failure()
        {

        }
    }
}
