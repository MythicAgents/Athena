extern alias shellplugin;

namespace CommandReliability.Tests;

[TestClass]
public sealed class ShellTests
{
    [TestMethod]
    public async Task ShellRunsAProcessAndReturnsItsOutput()
    {
        string script = Path.Combine(Path.GetTempPath(), $"athena-shell-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(script, "#!/bin/sh\nprintf smoke-output\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var messages = new RecordingMessageManager();
            var runner = new shellplugin::Agent.ProcessRunner(script, "shell", messages);

            runner.Start();
            await messages.WaitForTerminalResponse("shell", TimeSpan.FromSeconds(2));

            Assert.IsTrue(messages.Interactions.Any(message =>
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(message.data)).Contains("smoke-output")));
        }
        finally { File.Delete(script); }
    }
}
