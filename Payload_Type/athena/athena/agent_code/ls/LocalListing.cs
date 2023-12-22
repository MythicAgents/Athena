
using Agent.Models;
using System.Net;

namespace Agent
{
    internal class LocalListing
    {
        internal static FileBrowserResponseResult GetLocalListing(string path, string task_id)
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
                    return new FileBrowserResponseResult();
                }

                if (baseFileInfo.Attributes.HasFlag(FileAttributes.Directory))
                {
                    return GetLocalDirectoryListing(path, task_id);
                }

                return GetSingleFileLocalListingResult(path, task_id);
            }
            catch (UnauthorizedAccessException e)
            {
                return new FileBrowserResponseResult();
            }
        }

        internal static FileBrowserResponseResult GetSingleFileLocalListingResult(string path, string task_id)
        {
            DirectoryInfo file = new DirectoryInfo(path);
            var files = new List<FileBrowserFile> { LsUtilities.GetFile(path) };
            var result = new FileBrowserResponseResult()
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

        private static FileBrowserResponseResult GetLocalDirectoryListing(string path, string task_id)
        {
            DirectoryInfo file = new DirectoryInfo(path);
            var files = LsUtilities.GetFiles(path).ToList<FileBrowserFile>();
            var result = new FileBrowserResponseResult()
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
                    access_time = LsUtilities.GetTimeStamp(new DateTimeOffset(file.LastAccessTime).ToUnixTimeMilliseconds()),
                    modify_time = LsUtilities.GetTimeStamp(new DateTimeOffset(file.LastWriteTime).ToUnixTimeMilliseconds()),
                    size = 0,
                    files = files,
                },
            };

            if (files.Count > 0)
            {
                result.process_response = new Dictionary<string, string> { { "message", $"0x28" } };
            }
            else
            {
                result.process_response = new Dictionary<string, string> { { "message", $"0x29" } };
            }

            return result;

        }
    }
}
