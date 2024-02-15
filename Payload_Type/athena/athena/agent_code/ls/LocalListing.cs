
using Agent.Models;
using System.Net;

namespace Agent
{
    internal class LocalListing
    {
        internal static FileBrowserTaskResponse GetLocalListing(string path, string task_id)
        {
            if (path == "." || string.IsNullOrEmpty(path))
            {
                path = Directory.GetCurrentDirectory();
            }

            try
            {
                DirectoryInfo baseFileInfo = new DirectoryInfo(path);

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
                    return GetLocalDirectoryListing(path, task_id);
                }

                return GetSingleFileLocalListingResult(path, task_id);
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

        internal static FileBrowserTaskResponse GetSingleFileLocalListingResult(string path, string task_id)
        {
            DirectoryInfo file = new DirectoryInfo(path);
            var files = new List<FileBrowserFile> { LsUtilities.GetFile(path) };
            var result = new FileBrowserTaskResponse()
            {
                task_id = task_id,
                completed = true,
                file_browser = new FileBrowser
                {
                    host = Dns.GetHostName(),
                    is_file = false,
                    permissions = new Dictionary<string, string>(),
                    name = file.Name,
                    parent_path = LsUtilities.GetParentDirectory(file).TrimEnd(Path.DirectorySeparatorChar),
                    success = true,
                    update_deleted = false,
                    access_time = LsUtilities.GetTimeStamp(new DateTimeOffset(file.LastAccessTime).ToUnixTimeMilliseconds()),
                    modify_time = LsUtilities.GetTimeStamp(new DateTimeOffset(file.LastWriteTime).ToUnixTimeMilliseconds()),
                    size = 0,
                    files = files,
                },
            };

            if(file.Exists)
            {
                result.process_response = new Dictionary<string, string> { { "message", $"0x28" } };
            }
            else
            {
                result.process_response = new Dictionary<string, string> { { "message", $"0x29" } };
            }

            return result;
        }

        private static FileBrowserTaskResponse GetLocalDirectoryListing(string path, string task_id)
        {
            DirectoryInfo file = new DirectoryInfo(path);
            var files = LsUtilities.GetFiles(path).ToList<FileBrowserFile>();
            var result = new FileBrowserTaskResponse()
            {
                task_id = task_id,
                completed = true,
                file_browser = new FileBrowser
                {
                    host = Dns.GetHostName(),
                    is_file = false,
                    permissions = new Dictionary<string, string>(),
                    name = file.Name,
                    parent_path = LsUtilities.GetParentDirectory(file).TrimEnd(Path.DirectorySeparatorChar),
                    success = true,
                    update_deleted = true,
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
