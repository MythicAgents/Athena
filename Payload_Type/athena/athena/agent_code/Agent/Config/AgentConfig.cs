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
            uuid = "2f11aea4-ecad-47e3-9a87-eb7027b0daed";
            psk = "b68isf2NDItXE368gAPN2eKTYnIg5WURwsQY/oQnOp4=";
        }

        public event EventHandler? SetAgentConfigUpdated;
    }
}
