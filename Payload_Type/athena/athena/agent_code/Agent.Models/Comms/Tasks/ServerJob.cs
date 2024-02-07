using Agent.Interfaces;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    /// <summary>
    /// Base object to track Athena tasks
    /// </summary>
    public class ServerJob
    {
        public bool started { get; set; }
        public bool complete { get; set; }
        public ServerTask task { get; set; }
        public CancellationTokenSource cancellationtokensource { get; set; }

        public ServerJob() { }

        public ServerJob(ServerTask task)
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
    public class ServerDownloadJob : ServerJob
    {
        public string file_id { get; set; }
        public int total_chunks { get; set; }
        public int chunk_num { get; set; }
        public int chunk_size { get; set; }
        public string path { get; set; }
        public long bytesRead { get; set; }

        public ServerDownloadJob(ServerJob job, string path, int chunk_size)
        {
            this.task = job.task;
            this.chunk_size = chunk_size;
            this.started = job.started;
            this.complete = job.complete;
            this.cancellationtokensource = new CancellationTokenSource();
            this.chunk_num = 0;
            this.path = path.Replace("\"", string.Empty);
        }
    }
    
    /// <summary>
    /// An object to track Athena upload tasks
    /// </summary>
    public class ServerUploadJob : ServerJob
    {
        public string file_id { get; set; }
        public int total_chunks { get; set; }
        public int chunk_num { get; set; }
        public int chunk_size { get; set; } = 512000;
        public string path { get; set; }

        public ServerUploadJob(ServerJob job, int chunk_size)
        {
            this.task = job.task;
            this.chunk_size = chunk_size;
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
