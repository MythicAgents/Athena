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
        [TestMethod]
        public void LoadAssemblyAsync_Success()
        {
            //// Arrange
            //IAssemblyManager assemblyManager = new AssemblyManager(_messageManager, _logger, _config, _tokenManager);
            //string taskId = "123";
            //byte[] assemblyBytes = new byte[] { /* your assembly bytes here */ };

            //// Act
            //bool result = assemblyManager.LoadAssemblyAsync(taskId, assemblyBytes);

            //// Assert
            //Assert.IsTrue(result);

            //To Implement
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void LoadPluginAsync_Success()
        {
            // Arrange
            IAssemblyManager assemblyManager = new AssemblyManager(_messageManager, _logger, _config, _tokenManager);
            string taskId = "123";
            string pluginName = "SamplePlugin";
            string dllRelativePath = "../../../../ds/bin/Debug/net7.0/ds.dll";
            Assert.IsTrue(File.Exists(dllRelativePath));

            var buf = File.ReadAllBytes(dllRelativePath);

            // Act
            bool result = assemblyManager.LoadPluginAsync(taskId, pluginName, buf);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TryGetPluginReflection_Success()
        {
            //// Arrange
            //IAssemblyManager assemblyManager = new AssemblyManager(_messageManager, _logger, _config, _tokenManager);
            //string pluginName = "ds";
            //IPlugin expectedPlugin = new ds.Ds(_messageManager, _config, _logger, _tokenManager);
            //// Assuming you have a concrete implementation of IPlugin

            //string dllRelativePath = "../../../../ds/bin/Debug/net7.0/ds.dll";
            //Assert.IsTrue(File.Exists(dllRelativePath));

            //var buf = File.ReadAllBytes(dllRelativePath);


            //// Act
            //assemblyManager.LoadPluginAsync("123", pluginName, buf);
            //bool result = assemblyManager.TryGetPlugin(pluginName, out IPlugin? actualPlugin);

            //// Assert
            //Assert.IsTrue(result);
            //Assert.IsNotNull(actualPlugin);
            //Assert.AreSame(expectedPlugin.Name, actualPlugin.Name);
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
            IAssemblyManager assemblyManager = new AssemblyManager(_messageManager, _logger, _config, _tokenManager);
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

        // Repeat similar tests for TryGetPlugin with IFilePlugin, IProxyPlugin, and IForwarderPlugin
    }
}
