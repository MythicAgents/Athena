using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace upload
{
    public class UploadArgs
    {
        public string path { get; set; }
        public string filename { get; set; }
        public string file { get; set; }
        public bool Validate(out string message)
        {
            message = String.Empty;

            //If we didn't get a path set it to the current directory
            if (string.IsNullOrEmpty(path) || path == ".")
            {
                path = Directory.GetCurrentDirectory();
            }

            if(!Directory.Exists(path))
            {
                message = $"Directory doesn't exist: {path}";
                return false;
            }

            if (string.IsNullOrEmpty(filename))
            {
                message = "No filename specified";
                return false;
            }

            path = Path.Combine(path, filename);    
            return true;
        }
    }
}
