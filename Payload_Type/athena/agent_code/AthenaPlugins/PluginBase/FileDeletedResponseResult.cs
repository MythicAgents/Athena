namespace PluginBase
{
    public class FileDeletedResponseResult
    {
        List<DeletedFile> removed_files { get; set; }
    }
    public class DeletedFile
    {
        public string host { get; set; }
        public string path { get; set; }
    }
}
