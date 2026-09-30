namespace Agent.Interfaces
{
    public interface IAgentConfig
    {
        int chunk_size { get; set; }
        string? uuid { get; set; }
        string build_uuid { get; }
        bool require_plugin_contract_fingerprint => false;
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
