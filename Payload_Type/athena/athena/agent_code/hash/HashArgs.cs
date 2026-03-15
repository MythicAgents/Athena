namespace hash
{
    public class HashArgs
    {
        public string action { get; set; } = "hash";
        public string path { get; set; } = "";
        public string algorithm { get; set; } = "sha256";
        public bool encode { get; set; } = true;
    }
}
