using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Agent
{
    public class ConfigUpdateArgs
    {
        public int sleep { get; set; } = -1;
        public int jitter { get; set; } = -1;
        public string killdate { get; set; } = "01/01/0001";
        public int chunk_size { get; set; } = -1;
        public string prettyOutput { get; set; }
        public int inject { get; set; } = -1;
        public string debug { get; set; }
    }
}
