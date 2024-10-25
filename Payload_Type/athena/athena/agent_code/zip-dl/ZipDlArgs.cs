namespace Agent
{
    public class ZipDlArgs
    {
        public string source { get; set; }
        public string destination { get; set; }
        public bool write { get; set; } = false; //Write to disk first before downloading manually, otherwise hold in memory.
        //public bool verbose { get; set; } = false;
        public bool force { get; set; } = false;    
        public bool Validate()
        {
            if (this.write)
            {
                if (string.IsNullOrEmpty(destination))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
