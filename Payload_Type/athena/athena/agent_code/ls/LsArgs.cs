using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ls
{
    public class LsArgs
    {
        public string path { get; set; } = string.Empty;
        public string file { get; set; } = string.Empty;
        public string host { get; set; } = string.Empty;
        public bool Validate()
        {
            if (string.IsNullOrEmpty(this.path))
            {
                this.path = Directory.GetCurrentDirectory();
            }

            if (!string.IsNullOrEmpty(this.file))
            {
                this.path = Path.Combine(this.path, this.file);
            }
            return true;
        }
    }
}
