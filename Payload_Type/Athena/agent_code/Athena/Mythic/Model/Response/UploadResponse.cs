namespace Athena.Mythic.Model.Response
{
    public class UploadResponse : ResponseResult {
        public UploadResponseData upload { get; set; }
    }

    public class UploadResponseData
    {
        public int chunk_size { get; set; }
        public int chunk_num { get; set; }
        public string file_id { get; set; }
        public string full_path { get; set; }
    }

    public class UploadResponseResponse : ResponseResult
    {
        public int total_chunks { get; set; }
        public string full_path { get; set; }
        public int chunk_num { get; set; }
        public string chunk_data { get; set; }
    }
}

