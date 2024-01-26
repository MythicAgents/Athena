namespace Agent.Interfaces
{
    public interface IAgentConfig
    {
        int chunk_size { get; set; }
        string? uuid { get; set; }
        int sleep { get; set; }
        int jitter { get; set; }
        string? psk { get; set; }
        bool prettyOutput { get; set; }
        bool debug { get; set; }
        int inject { get; set; }
        DateTime killDate { get; set; }
        public event EventHandler? SetAgentConfigUpdated;
    }
}
