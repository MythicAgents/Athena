using Athena.Models.Responses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LsUtilities
{
    public class RemoteListing
    {
        public static FileBrowserResponseResult GetRemoteListing(string path, string host, string task_id)
        {
            try
            {
                UNCPathParser parser = new UNCPathParser(path);
                DirectoryInfo baseFileInfo = new DirectoryInfo(parser.FullPath);

                if (!baseFileInfo.Exists)
                {
                    return new FileBrowserResponseResult()
                    {
                        task_id = task_id,
                    };
                }

                if (baseFileInfo.Attributes.HasFlag(FileAttributes.Directory))
                {
                    return GetRemoteDirectory(parser, task_id, host);
                }

                return GetRemoteSingleFile(parser, task_id, host);    
            }
            catch
            {
                return new FileBrowserResponseResult();
            }
        }
        private static FileBrowserResponseResult GetRemoteSingleFile(UNCPathParser parser, string host, string task_id)
        {
            DirectoryInfo file = new DirectoryInfo(parser.FullPath);
            var files = new List<FileBrowserFile> { LsUtilities.GetFile(parser.FullPath) };
            var result = new FileBrowserResponseResult()
            {
                task_id = task_id,
                completed = true,
                file_browser = new FileBrowser
                {
                    host = host,
                    is_file = false,
                    permissions = new Dictionary<string, string>(),
                    name = parser.GetFileName(),
                    parent_path = parser.GetParentPath(),
                    success = true,
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

        private static FileBrowserResponseResult GetRemoteDirectory(UNCPathParser parser, string task_id, string host)
        {
            DirectoryInfo file = new DirectoryInfo(parser.FullPath);
            var files = LsUtilities.GetFiles(parser.FullPath).ToList<FileBrowserFile>();
            var result = new FileBrowserResponseResult()
            {
                task_id = task_id,
                completed = true,
                file_browser = new FileBrowser
                {
                    host = host,
                    is_file = false,
                    permissions = new Dictionary<string, string>(),
                    name = parser.GetFileName(),
                    parent_path = parser.GetParentPath(),
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
