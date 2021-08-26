using System.Threading;

namespace Athena.Mythic.Model
{
    public class MythicJob
    {
        public bool started { get; set; }
        public bool complete { get; set; }
        public bool hasoutput { get; set; }
        public bool errored { get; set; }
        public string taskresult { get; set; }
        //Will use this to determine if a long running assembly is still executing or not.
        //If it hasn't printed anything to console in a while, then after a few attempts we'll have to assume that it's been completed.
        public int resultpasses { get; set; }
        public MythicTask task { get; set; }
        public CancellationTokenSource cancellationtokensource { get; set; }

        public MythicJob() { }

        public MythicJob(MythicTask task)
        {
            this.task = task;
            this.started = false;
            this.complete = false;
            this.hasoutput = false;
            this.resultpasses = 0;
            this.cancellationtokensource = new CancellationTokenSource();
        }

    }
    public class MythicDownloadJob : MythicJob
    {
        public string file_id { get; set; }
        public int total_chunks { get; set; }
        public int chunk_num { get; set; }
        public long file_size { get; set; }
        public int chunk_size { get; set; } = 512000;
        public string path { get; set; }
        public bool downloadStarted { get; set; }

        public MythicDownloadJob(MythicJob job)
        {
            this.task = job.task;
            this.chunk_size = 512000;
            this.started = job.started;
            this.complete = job.complete;
            this.hasoutput = job.hasoutput;
            this.errored = job.errored;
            this.taskresult = job.taskresult;
            this.resultpasses = job.resultpasses;
            this.cancellationtokensource = new CancellationTokenSource();
            this.downloadStarted = false;
        }
    }
}
