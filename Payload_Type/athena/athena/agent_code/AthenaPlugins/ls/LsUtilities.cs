using Athena.Models.Responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LsUtilities
{
    public static class LsUtilities
    {
        public static FileBrowserFile GetFile(string path)
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
        public static ConcurrentBag<FileBrowserFile> GetFiles(string path)
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
        public static string GetFileName(string path)
        {
            return System.IO.Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        }

        public static UInt64 GetTimeStamp(long timestamp)
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
        public static string GetParentDirectory(DirectoryInfo file)
        {
            if (file.Parent is null)
            {
                return "";
            }

            return file.Parent.FullName;
        }
    }
}
