using Agent.Interfaces;

namespace Agent.Managers
{
    public class LogManager : ILogger
    {
        private bool isDebugEnabled = false;

        public void SetDebug(bool debug) => this.isDebugEnabled = debug;

        public void Log(string message) => WriteFormatted($"[{DateTime.Now}] {message}");

        public void Debug(string message)
        {
            if (!this.isDebugEnabled)
                return;

            WriteFormatted($"[DEBUG][{DateTime.Now}] {message}");
        }

        private static void WriteFormatted(string line)
        {
            System.Diagnostics.Debug.WriteLine(line);
            Console.WriteLine(line);
        }
    }
}
