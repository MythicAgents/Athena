namespace clipboard
{
    public class ClipboardArgs
    {
        public string action { get; set; } = "get";
        public int duration { get; set; } = 60;
        public int interval { get; set; } = 2;
    }
}
