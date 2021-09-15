namespace Athena.Models.Mythic.Response
{
    public class DownloadResponse : ResponseResult
    {
        public int total_chunks { get; set; }
        public string full_path { get; set; }
        public int chunk_num { get; set; }
        public string chunk_data { get; set; }
    }
}
