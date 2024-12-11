using Agent.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tail
{
    public class TailArgs
    {
        public string path { get; set; } = string.Empty;
        public int lines { get; set; } = 5;
        public bool watch { get; set; } = false;

        public bool Validate(out string message)
        {
            message = string.Empty;
            if (string.IsNullOrEmpty(this.path))
            {
                message = "Please specify a path!";
                return false;
            }

            if (!File.Exists(this.path))
            {
                message = "File doesn't exist!";
                return false;

            }

            return true;
        }
    }
}
