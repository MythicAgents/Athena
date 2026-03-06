using Workflow.Contracts;

namespace Workflow.Providers
{
    public class DiagnosticService : ILogger
    {
        public void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now}] {message}");
            Console.WriteLine($"[{DateTime.Now}] {message}");
        }
    }
}
