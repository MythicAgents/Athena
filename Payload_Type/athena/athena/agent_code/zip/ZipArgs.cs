namespace Agent
{
    public class ZipArgs
    {
        public string source { get; set; } = "";
        public string destination { get; set; } = "";
        //public bool verbose { get; set; } = false;
        public bool Validate()
        {
            return !string.IsNullOrEmpty(this.source) && !string.IsNullOrEmpty(this.destination);
        }
    }
}
