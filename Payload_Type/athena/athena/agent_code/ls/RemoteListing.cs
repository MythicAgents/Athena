

using Agent.Models;

namespace Agent
{
    internal class RemoteListing
    {
        internal static FileBrowserTaskResponse GetRemoteListing(string path, string host, string task_id)
        {
            try
            {
                UNCPathParser parser = new UNCPathParser(path);
                DirectoryInfo baseFileInfo = new DirectoryInfo(parser.FullPath);

                if (!baseFileInfo.Exists)
                {
                    return new FileBrowserTaskResponse()
                    {
                        user_output = "Path doesn't exist!",
                        status = "error",
                        completed = true,
                        task_id = task_id
                    };
                }

                if (baseFileInfo.Attributes.HasFlag(FileAttributes.Directory))
                {
                    return GetRemoteDirectory(parser, task_id, host);
                }

                return GetRemoteSingleFile(parser, task_id, host);    
            }
            catch (Exception e)
            {
                return new FileBrowserTaskResponse()
                {
                    task_id = task_id,
                    user_output = e.ToString(),
                    status = "error",
                    completed = true,
                };
            }
        }
        internal static FileBrowserTaskResponse GetRemoteSingleFile(UNCPathParser parser, string host, string task_id)
        {
            DirectoryInfo file = new DirectoryInfo(parser.FullPath);
            var files = new List<FileBrowserFile> { LsUtilities.GetFile(parser.FullPath) };
            var result = new FileBrowserTaskResponse()
            {
                task_id = task_id,
                completed = true,
                file_browser = new FileBrowser
                {
                    host = host,
                    is_file = false,
                    permissions = new Dictionary<string, string>(),
                    name = parser.GetFileName(),
                    parent_path = parser.GetParentPath(true),
                    success = true,
                    update_deleted = false,
                    access_time = LsUtilities.GetTimeStamp(new DateTimeOffset(file.LastAccessTime).ToUnixTimeMilliseconds()),
                    modify_time = LsUtilities.GetTimeStamp(new DateTimeOffset(file.LastWriteTime).ToUnixTimeMilliseconds()),
                    size = 0,
                    files = files,
                },
            };

            if (file.Exists)
            {
                result.process_response = new Dictionary<string, string> { { "message", $"0x28" } };
            }
            else
            {
                result.process_response = new Dictionary<string, string> { { "message", $"0x29" } };
            }

            return result;
        }

        internal static FileBrowserTaskResponse GetRemoteDirectory(UNCPathParser parser, string task_id, string host)
        {
            DirectoryInfo file = new DirectoryInfo(parser.FullPath);
            var files = LsUtilities.GetFiles(parser.FullPath).ToList<FileBrowserFile>();
            var result = new FileBrowserTaskResponse()
            {
                task_id = task_id,
                completed = true,
                file_browser = new FileBrowser
                {
                    host = host,
                    is_file = false,
                    permissions = new Dictionary<string, string>(),
                    name = parser.GetFileName(),
                    parent_path = parser.GetParentPath(true),
                    update_deleted = true,
                    success = true,
                    access_time = LsUtilities.GetTimeStamp(new DateTimeOffset(file.LastAccessTime).ToUnixTimeMilliseconds()),
                    modify_time = LsUtilities.GetTimeStamp(new DateTimeOffset(file.LastWriteTime).ToUnixTimeMilliseconds()),
                    size = 0,
                    files = files,
                },
            };

            result.user_output = $"Returned {files.Count} files in File Browser";

            return result;
        }
    }
}
