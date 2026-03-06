using System.Diagnostics;

namespace Workflow.Contracts
{
    public static class DebugLog
    {
        [Conditional("DEBUG")]
        public static void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[DEBUG][{DateTime.Now}] {message}");
            Console.WriteLine(
                $"[DEBUG][{DateTime.Now}] {message}");
        }
    }
}
