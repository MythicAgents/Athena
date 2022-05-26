using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Models.Mythic.Tasks
{
    /// <summary>
    /// Base object to track Athena tasks
    /// </summary>
    public class MythicJob
    {
        public bool started { get; set; }
        public bool complete { get; set; }
        public bool hasoutput { get; set; }
        public bool errored { get; set; }
        public string taskresult { get; set; }
        public MythicTask task { get; set; }
        public CancellationTokenSource cancellationtokensource { get; set; }

        public MythicJob() { }

        public MythicJob(MythicTask task)
        {
            this.task = task;
            this.started = false;
            this.complete = false;
            this.hasoutput = false;
            this.cancellationtokensource = new CancellationTokenSource();
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
        public bool downloadStarted { get; set; }
        public long bytesRead { get; set; }

        public MythicDownloadJob(MythicJob job)
        {
            this.task = job.task;
            this.chunk_size = 512000;
            this.started = job.started;
            this.complete = job.complete;
            this.hasoutput = job.hasoutput;
            this.errored = job.errored;
            this.taskresult = job.taskresult;
            this.cancellationtokensource = new CancellationTokenSource();
            this.downloadStarted = false;
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
        public bool uploadStarted { get; set; }

        public MythicUploadJob(MythicJob job)
        {
            this.task = job.task;
            this.chunk_size = 512000;
            this.started = job.started;
            this.complete = job.complete;
            this.hasoutput = job.hasoutput;
            this.errored = job.errored;
            this.taskresult = job.taskresult;
            this.cancellationtokensource = new CancellationTokenSource();
            this.uploadStarted = false;
            this.chunk_num = 0;
        }
    }
}
