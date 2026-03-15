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

        /// Fire-and-forget plugin execution, wait only on the response event.
        /// Task.Run uses thread pool (background) threads so hanging plugins
        /// won't prevent the test process from exiting.
        protected Task<TaskResponse> ExecuteAndGetResponse(
            ServerJob job, int timeoutSeconds = 30)
        {
            _ = Task.Run(() => _plugin.Execute(job));

            bool signaled = _messageManager.hasResponse.WaitOne(
                TimeSpan.FromSeconds(timeoutSeconds));
            if (!signaled)
            {
                Assert.Fail(
                    $"No response within {timeoutSeconds}s " +
                    $"for command '{job.task.command}'");
            }

            string response = _messageManager.GetRecentOutput();
            return Task.FromResult(
                JsonSerializer.Deserialize<TaskResponse>(response));
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
