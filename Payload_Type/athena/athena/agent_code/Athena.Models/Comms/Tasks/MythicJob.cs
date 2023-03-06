using System.Text.Json.Serialization;
using System.Threading;

namespace Athena.Models.Mythic.Tasks
{
    /// <summary>
    /// Base object to track Athena tasks
    /// </summary>
    public class MythicJob
    {
        public bool started { get; set; }
        public bool complete { get; set; }
        public MythicTask task { get; set; }
        public CancellationTokenSource cancellationtokensource { get; set; }

        public MythicJob() { }

        public MythicJob(MythicTask task)
        {
            this.task = task;
            this.started = false;
            this.complete = false;
            this.cancellationtokensource = new CancellationTokenSource();
        }
        public JobStatus GetStatus()
        {

            return new JobStatus()
            {
                id = this.task.id,
                status = this.started ? "started" : "queued",
                command = this.task.command
            };
        }

    }
    
    /// <summary>
    /// An object to track Athena download tasks
    /// </summary>
    public class MythicDownloadJob : MythicJob
    {
        public string file_id { get; set; }
        public int total_chunks { get; set; }
        public int chunk_num { get; set; }
        public int chunk_size { get; set; } = 512000;
        public string path { get; set; }
        public long bytesRead { get; set; }

        public MythicDownloadJob(MythicJob job)
        {
            this.task = job.task;
            this.chunk_size = 512000;
            this.started = job.started;
            this.complete = job.complete;
            this.cancellationtokensource = new CancellationTokenSource();
            this.chunk_num = 0;
        }
    }
    
    /// <summary>
    /// An object to track Athena upload tasks
    /// </summary>
    public class MythicUploadJob : MythicJob
    {
        public string file_id { get; set; }
        public int total_chunks { get; set; }
        public int chunk_num { get; set; }
        public int chunk_size { get; set; } = 512000;
        public string path { get; set; }

        public MythicUploadJob(MythicJob job)
        {
            this.task = job.task;
            this.chunk_size = 512000;
            this.started = job.started;
            this.complete = job.complete;
            this.cancellationtokensource = new CancellationTokenSource();
            this.chunk_num = 0;
        }
    }
    public class JobStatus
    {
        public string id { get; set; }
        public string status { get; set; }
        public string command { get; set; }

    }
    [JsonSerializable(typeof(List<JobStatus>))]
    public partial class JobStatusContext : JsonSerializerContext
    {
    }
}
