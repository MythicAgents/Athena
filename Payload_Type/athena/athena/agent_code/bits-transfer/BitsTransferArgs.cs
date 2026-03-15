namespace bits_transfer
{
    public class BitsTransferArgs
    {
        public string action { get; set; } = "download";
        public string url { get; set; } = "";
        public string path { get; set; } = "";
        public string job_name { get; set; } = "AthenaTransfer";
    }
}
