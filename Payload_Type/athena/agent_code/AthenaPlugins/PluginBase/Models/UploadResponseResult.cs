namespace Athena.Plugins
{
    public class UploadResponse : ResponseResult
    {
        public UploadResponseData upload { get; set; }
    }

    public class UploadResponseData
    {
        public int chunk_size { get; set; }
        public int chunk_num { get; set; }
        public string file_id { get; set; }
        public string full_path { get; set; }
    }
}
