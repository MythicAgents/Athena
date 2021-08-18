namespace Athena.Mythic.Model
{
    public class MythicTask
    {
        public string command { get; set; }

        //Can this be a Dictionary<string,object> instead?
        public string parameters { get; set; }
        //public Dictionary<string,object> parameters { get; set; }
        public string id { get; set; }
        public string timestamp { get; set; }
        
        //public string task_id { get; set; }
        //public string result { get; set; }
        //public bool completed { get; set; }
        //public bool started { get; set; }
        //public bool error { get; set; }
        //public MythicJob job { get; set; }

        //public int job_id { get; set; }
        //public bool success { get; set; }
        //public string response { get; set; }
        //public Thread thread { get; set; }
        //public bool upload { get; set; }
        //public bool download { get; set; }
        //public bool chunking_started { get; set; }
        //public int total_chunks { get; set; }
        //public int chunk_num { get; set; }
        //public int write_num { get; set; }
        //public string file_id { get; set; }
        //public long file_size { get; set; }
        //public string path { get; set; }
        //public List<string> chunks { get; set; }

        //public MythicTask()
        //{
        //    chunks = new List<string>();
        //}
    }
}
