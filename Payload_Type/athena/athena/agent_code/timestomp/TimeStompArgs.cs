using System.Net.Http.Headers;

namespace Agent
{
    public class TimeStompArgs
    {
        public string source { get; set; }
        public string destination { get; set; }

        public bool Validate(out string message)
        {
            message = String.Empty;
            if (string.IsNullOrEmpty(source))
            {
                message = "Missing source file!";
                return false;
            }

            if (string.IsNullOrEmpty(this.destination))
            {
                message = "Missing destination file!";
                return false;
            }

            if (!File.Exists(this.source))
            {
                message = "Source file doesn't exist!";
                return false;
            }

            if (!File.Exists(this.destination))
            {
                message = "Destination file doesn't exist!";
                return false;
            }

            return true;

        }
    }
}
