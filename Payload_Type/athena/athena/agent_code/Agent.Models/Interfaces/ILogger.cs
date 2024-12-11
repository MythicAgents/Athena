namespace Agent.Interfaces
{
    public interface ILogger
    {
        public void SetDebug(bool debug);
        public void Log(string message);
        public void Debug(string message);
    }
}
