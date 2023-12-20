using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Models
{
    public class DownloadArgs
    {
        public string host { get; set; }
        public string file { get; set; }
        public int chunk_size { get; set; } = 85000;
    }
}
