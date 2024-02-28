
using Agent.Models;
using System.Collections.Concurrent;

namespace Agent
{
    internal static class LsUtilities
    {
        internal static FileBrowserFile GetFile(string path)
        {
            FileInfo fInfo = new FileInfo(path);

            if (!fInfo.Exists)
            {
                return null;
            }

            return new FileBrowserFile
            {
                is_file = true,
                permissions = new Dictionary<string, string>(),
                name = GetFileName(path),
                access_time = GetTimeStamp(new DateTimeOffset(new FileInfo(path).LastAccessTime).ToUnixTimeMilliseconds()),
                modify_time = GetTimeStamp(new DateTimeOffset(new FileInfo(path).LastWriteTime).ToUnixTimeMilliseconds()),
                size = new FileInfo(path).Length,
            };
        }

        //This should only be hit when we actually have a directory
        internal static ConcurrentBag<FileBrowserFile> GetFiles(string path)
        {
            ConcurrentBag<FileBrowserFile> files = new ConcurrentBag<FileBrowserFile>();
            FileInfo parentFileInfo = new FileInfo(path);
            DirectoryInfo parentDirectoryInfo = new DirectoryInfo(parentFileInfo.FullName);
            foreach (var fInfo in parentDirectoryInfo.GetFileSystemInfos())
            {
                var file = new FileBrowserFile
                {
                    is_file = !fInfo.Attributes.HasFlag(FileAttributes.Directory),
                    permissions = new Dictionary<string, string>(),
                    name = GetFileName(fInfo.FullName),
                    access_time = GetTimeStamp(new DateTimeOffset(parentDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
                    modify_time = GetTimeStamp(new DateTimeOffset(parentDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
                };

                if (file.is_file)
                {
                    file.size = new FileInfo(fInfo.FullName).Length;
                }
                else
                {
                    file.size = 0;
                }

                files.Add(file);
            }
            return files;
        }
        internal static string GetFileName(string path)
        {
            return System.IO.Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        }

        internal static UInt64 GetTimeStamp(long timestamp)
        {
            try
            {
                return Convert.ToUInt64(timestamp);
            }
            catch
            {
                return 0;
            }
        }
        internal static string GetParentDirectory(DirectoryInfo file)
        {
            if (file.Parent is null)
            {
                return "";
            }

            if (OperatingSystem.IsWindows())
            {
                return file.Parent.FullName.TrimEnd(Path.DirectorySeparatorChar);
            }

            return file.Parent.FullName;
        }
    }
}
