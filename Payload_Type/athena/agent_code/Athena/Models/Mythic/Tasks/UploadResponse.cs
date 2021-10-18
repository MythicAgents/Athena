namespace Athena.Models.Mythic.Response
{
    public class UploadResponse : ResponseResult {
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

