using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Athena.Plugins;

namespace Athena.Plugins
{
    public class DownloadResponse : ResponseResult
    {
        public int total_chunks { get; set; }
        public string full_path { get; set; }
        public int chunk_num { get; set; }
        public string chunk_data { get; set; }
    }
}
