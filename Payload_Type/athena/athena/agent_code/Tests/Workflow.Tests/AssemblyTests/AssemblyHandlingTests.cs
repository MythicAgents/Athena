using Workflow.Providers;
using Workflow.Tests.TestClasses;
using Workflow.Tests.TestInterfaces;

namespace Workflow.Tests.AssemblyTests
{
    [TestClass]
    public class ComponentProviderTests
    {
        IEnumerable<IChannel> _profiles = new List<IChannel>() { new TestProfile() };
        IRequestDispatcher _taskManager = new TestRequestDispatcher();
        ILogger _logger = new TestLogger();
        IServiceConfig _config = new TestServiceConfig();
        ICredentialProvider _tokenManager = new TestCredentialProvider();
        ISecurityProvider _cryptoManager = new TestCryptoManager();
        IDataBroker _messageManager = new TestDataBroker();
        IRuntimeExecutor _spawner = new TestSpawner();
        [TestMethod]
        public void LoadAssemblyAsync_Success()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "cat", "bin", "Debug", "net8.0", "cat.dll");

            // Arrange
            IComponentProvider assemblyManager = new ComponentProvider(_messageManager, _logger, _config, _tokenManager, _spawner, null);
            string taskId = "123";
            byte[] assemblyBytes = File.ReadAllBytes(path);

            // Act
            bool result = assemblyManager.LoadAssemblyAsync(taskId, assemblyBytes);

            // Assert
            Assert.IsTrue(result);

        }

        [TestMethod]
        public void LoadModuleAsync_Success()
        {
            // Arrange
            IComponentProvider assemblyManager = new ComponentProvider(_messageManager, _logger, _config, _tokenManager, _spawner, null);
            string taskId = "123";
            string moduleName = "SamplePlugin";
            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "cat", "bin", "Debug", "net8.0", "cat.dll");
            Assert.IsTrue(File.Exists(path));

            var buf = File.ReadAllBytes(path);

            // Act
            bool result = assemblyManager.LoadModuleAsync(taskId, moduleName, buf);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TryGetModuleReflection_Success()
        {
            //// Arrange
            IComponentProvider assemblyManager = new ComponentProvider(_messageManager, _logger, _config, _tokenManager, _spawner, null);
            string moduleName = "ds";
            PluginLoader loader = new PluginLoader(_messageManager);
            IModule expectedPlugin = loader.LoadPluginFromDisk(moduleName);
            // Assuming you have a concrete implementation of IModule

            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", moduleName, "bin", "Debug", "net8.0", $"{moduleName}.dll");
            Assert.IsTrue(File.Exists(path));

            var buf = File.ReadAllBytes(path);


            // Act
            assemblyManager.LoadModuleAsync("123", moduleName, buf);
            bool result = assemblyManager.TryGetModule(moduleName, out IModule? actualPlugin);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNotNull(actualPlugin);
            Assert.AreSame(expectedPlugin.Name, actualPlugin.Name);
        }
        [TestMethod]
        public void TryGetModuleReference_Success()
        {
            //// Arrange
            //IComponentProvider assemblyManager = new ComponentProvider(_messageManager, _logger, _config, _tokenManager);
            //string moduleName = "ds";
            //IModule expectedPlugin = new ds.Ds(_messageManager,_config, _logger, _tokenManager);
            //// Assuming you have a concrete implementation of IModule

            //// Act
            //assemblyManager.LoadModuleAsync("ds", moduleName, new byte[] { /* plugin bytes */ });
            //bool result = assemblyManager.TryGetModule(moduleName, out IModule? actualPlugin);

            //// Assert
            //Assert.IsTrue(result);
            //Assert.IsNotNull(actualPlugin);
            //Assert.AreSame(expectedPlugin.Name, actualPlugin.Name);
        }

        [TestMethod]
        public void TryGetModule_Failure()
        {
            // Arrange
            IComponentProvider assemblyManager = new ComponentProvider(_messageManager, _logger, _config, _tokenManager, _spawner, null);
            string nonExistentPluginName = "NonExistentPlugin";

            // Act
            bool result = assemblyManager.TryGetModule(nonExistentPluginName, out IModule? actualPlugin);

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
