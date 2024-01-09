namespace Agent.Models
{
    public class SpawnOptions
    {
        public string task_id { get; set; }
        public string commandline { get; set; }
        public string spoofedcommandline { get; set; }
        public int parent { get; set; }
        public bool output { get; set; }
        public bool suspended { get; set; } = false;
    }
}
