using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ls
{
    public class LsArgs
    {
        public string path { get; set; }
        public string file { get; set; }
        public string host { get; set; }
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
