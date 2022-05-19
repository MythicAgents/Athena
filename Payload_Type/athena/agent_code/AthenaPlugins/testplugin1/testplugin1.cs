using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Pluginbase;
using Newtonsoft.Json;
namespace Athena
{
    public static class Plugin
    {

        public static string Execute(Dictionary<string, object> args)
        {

            var ur = new UploadResponse()
            {
                upload = new UploadResponseData() { chunk_num = 1 }

            };

            return JsonConvert.SerializeObject(ur);

        }
    }
    public class ResponseResult
    {
        public string task_id;
        public string user_output;
        public string status;
        public string completed;
        public string file_id;
    }
    public class UploadResponse : ResponseResult
    {
        public UploadResponseData upload { get; set; }
    }

    //We send this to Mythic
    public class UploadResponseData
    {
        public int chunk_size { get; set; }
        public int chunk_num { get; set; }
        public string file_id { get; set; }
        public string full_path { get; set; }
    }
}
