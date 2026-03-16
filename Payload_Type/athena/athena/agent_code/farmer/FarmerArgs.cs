namespace farmer
{
    public class FarmerArgs
    {
        public int port { get; set; } = 0;
        public bool downgrade { get; set; } = false;
        public string serverHeader { get; set; } = "Microsoft-IIS/10.0";
        public string bindAddress { get; set; } = "";
    }
}
