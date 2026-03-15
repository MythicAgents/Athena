namespace event_log
{
    public class EventLogArgs
    {
        public string action { get; set; } = "query";
        public string log_name { get; set; } = "Application";
        public int count { get; set; } = 10;
        public string provider { get; set; } = "";
    }
}
