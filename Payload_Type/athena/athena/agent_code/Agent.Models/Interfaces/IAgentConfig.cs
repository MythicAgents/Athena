namespace Agent.Interfaces
{
    public interface IAgentConfig
    {
        string? uuid { get; set; }
        int sleep { get; set; }
        int jitter { get; set; }
        string? psk { get; set; }
        DateTime killDate { get; set; }
        public event EventHandler? SetAgentConfigUpdated;
    }
}
