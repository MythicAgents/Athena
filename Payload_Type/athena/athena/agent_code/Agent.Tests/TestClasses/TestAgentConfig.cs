using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestClasses
{
    internal class TestAgentConfig : IAgentConfig
    {
        public string? uuid { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public string? psk { get; set; }
        public DateTime killDate { get; set; }

        public event EventHandler? SetAgentConfigUpdated;

        public TestAgentConfig()
        {
            this.uuid = Guid.NewGuid().ToString();
            this.sleep = 10;
            this.jitter = 10;
            this.psk = "testpsk";
            this.killDate = DateTime.Now.AddYears(1);
        }
    }
}
