using Workflow.Contracts;
using Workflow.Models;
using Workflow.Tests.TestClasses;
using System.Text.Json;

namespace Workflow.Tests
{
    public abstract class PluginTestBase
    {
        protected IModule _plugin;
        protected TestDataBroker _messageManager;
        protected PluginLoader _pluginLoader;

        protected void LoadPlugin(string moduleName)
        {
            _messageManager = new TestDataBroker();
            _pluginLoader = new PluginLoader(_messageManager);
            _plugin = _pluginLoader.LoadPluginFromDisk(moduleName);
            Assert.IsNotNull(_plugin, $"Failed to load plugin: {moduleName}");
        }

        protected ServerJob CreateJob(
            string command, object parameters, string taskId = "test-1")
        {
            return new ServerJob()
            {
                task = new ServerTask()
                {
                    id = taskId,
                    parameters = JsonSerializer.Serialize(parameters),
                    command = command
                }
            };
        }

        /// Note: hasResponse is an AutoResetEvent that triggers on each Write/AddTaskResponse.
        /// For plugins that emit multiple intermediate responses, call Execute first then
        /// use GetRecentOutput() which returns the last response in the list.
        protected async Task<TaskResponse> ExecuteAndGetResponse(ServerJob job)
        {
            await _plugin.Execute(job);
            _messageManager.hasResponse.WaitOne(TimeSpan.FromSeconds(10));
            string response = _messageManager.GetRecentOutput();
            return JsonSerializer.Deserialize<TaskResponse>(response);
        }

        protected void AssertSuccess(TaskResponse response)
        {
            Assert.IsNotNull(response);
            Assert.AreNotEqual("error", response.status);
        }

        protected void AssertError(TaskResponse response)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual("error", response.status);
        }

        protected void AssertOutputContains(TaskResponse response, string expected)
        {
            Assert.IsNotNull(response);
            Assert.IsTrue(
                response.user_output?.Contains(expected) == true,
                $"Expected output to contain '{expected}', got: '{response.user_output}'"
            );
        }
    }
}
