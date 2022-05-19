using System;
using System.Collections.Generic;

namespace Athena
{
    public static class Plugin
    {

        public static DownloadResponse Execute(Dictionary<string, object> args)
        {
            var dr = new DownloadResponse()
            {
                task_id = "second",
                total_chunks = 10,
                chunk_num = 10
            };

            return dr;

        }
    }
    public class DownloadResponse : ResponseResult
    {
        public int total_chunks { get; set; }
        public string full_path { get; set; }
        public int chunk_num { get; set; }
        public string chunk_data { get; set; }
    }
    public class ResponseResult
    {
        public string task_id;
        public string user_output;
        public string status;
        public string completed;
        public string file_id;
    }

}
