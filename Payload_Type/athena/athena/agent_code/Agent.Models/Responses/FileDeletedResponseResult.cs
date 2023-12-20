namespace Agent.Models
{
    public class FileDeletedResponseResult : ResponseResult
    {
        List<DeletedFile> removed_files { get; set; }
        public string ToJson()
        {
            return "";
        }
    }
    public class DeletedFile
    {
        public string host { get; set; }
        public string path { get; set; }
    }
}
