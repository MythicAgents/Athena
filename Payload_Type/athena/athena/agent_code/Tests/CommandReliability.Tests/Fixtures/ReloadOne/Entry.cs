using Agent.Interfaces;

namespace ReloadOne;

public static class Entry
{
    public static void Run(string taskId, List<string> arguments, IMessageManager messageManager) =>
        messageManager.Write("version-one", taskId, true);

    public static void Throw(string taskId, List<string> arguments, IMessageManager messageManager) =>
        throw new InvalidOperationException("fixture invocation failure");
}
