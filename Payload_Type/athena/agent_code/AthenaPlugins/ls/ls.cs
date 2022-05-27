using PluginBase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Athena
{
    public static class Plugin
    {
        public static FileBrowserResponseResult Execute(Dictionary<string, object> args)
        {
            ConcurrentBag<FileBrowserFile> files = new ConcurrentBag<FileBrowserFile>();

            if (args["path"] is null)
            {
                return new FileBrowserResponseResult
                {
                    user_output = "File/Folder not found!",
                    completed = "true",
                    status = "error",
                    task_id = (string)args["task-id"]
                };
            }
            else
            {

                if (!File.Exists((string)args["path"]) && !Directory.Exists((string)args["path"]))
                {
                    return new FileBrowserResponseResult
                    {
                        user_output = "File/Folder not found!",
                        completed = "true",
                        status = "error",
                        task_id = (string)args["task-id"]
                    };
                }
                FileInfo parentFileInfo = new FileInfo((string)args["path"]);
                if (parentFileInfo.Attributes.HasFlag(FileAttributes.Directory))
                {
                    Console.WriteLine("true");
                }


                    return new FileBrowserResponseResult
                {
                    user_output = "File/Folder found!",
                    completed = "true",
                    status = "error",
                    task_id = (string)args["task-id"]
                };
            }
        }

        //public static FileBrowserResponseResult Execute(Dictionary<string, object> args)
        //{
        //    ConcurrentBag<FileBrowserFile> files = new ConcurrentBag<FileBrowserFile>();
        //    Console.WriteLine("In LS");

        //    if (args["path"] is not null)
        //    {
        //        if (!File.Exists((string)args["path"]) && !Directory.Exists((string)args["path"]))
        //        {
        //            return new FileBrowserResponseResult
        //            {
        //                user_output = "File/Folder not found!",
        //                completed = "true",
        //                status = "error",
        //                task_id = (string)args["task-id"]
        //            };
        //        }

        //        try
        //        {
        //            FileInfo parentFileInfo = new FileInfo((string)args["path"]);
        //            if (parentFileInfo.Attributes.HasFlag(FileAttributes.Directory))
        //            {
        //                DirectoryInfo parentDirectoryInfo = new DirectoryInfo(parentFileInfo.FullName);

        //                foreach (var fInfo in parentDirectoryInfo.GetFileSystemInfos())
        //                {
        //                    var file = new FileBrowserFile
        //                    {
        //                        is_file = !fInfo.Attributes.HasFlag(FileAttributes.Directory),
        //                        permissions = new Dictionary<string, string>(),
        //                        name = fInfo.Name,
        //                        access_time = new DateTimeOffset(parentDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
        //                        modify_time = new DateTimeOffset(parentDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
        //                    };

        //                    if (file.is_file)
        //                    {
        //                        file.size = new FileInfo(fInfo.FullName).Length;
        //                    }
        //                    else
        //                    {
        //                        file.size = 0;
        //                    }

        //                    files.Add(file);
        //                }

        //                if (parentDirectoryInfo.Parent is null)
        //                {
        //                    return new FileBrowserResponseResult
        //                    {
        //                        task_id = (string)args["task-id"],
        //                        completed = "true",
        //                        user_output = "done",
        //                        file_browser = new FileBrowser
        //                        {
        //                            host = Dns.GetHostName(),
        //                            is_file = false,
        //                            permissions = new Dictionary<string, string>(),
        //                            name = parentDirectoryInfo.Name,
        //                            parent_path = "",
        //                            success = true,
        //                            access_time = new DateTimeOffset(parentDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
        //                            modify_time = new DateTimeOffset(parentDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
        //                            size = 0,
        //                            files = files.ToList()
        //                        },
        //                    };
        //                }
        //                else
        //                {
        //                    return new FileBrowserResponseResult
        //                    {
        //                        task_id = (string)args["task-id"],
        //                        completed = "true",
        //                        user_output = "done",
        //                        file_browser = new FileBrowser
        //                        {
        //                            host = Dns.GetHostName(),
        //                            is_file = false,
        //                            permissions = new Dictionary<string, string>(),
        //                            name = parentDirectoryInfo.Name,
        //                            parent_path = parentDirectoryInfo.Parent.FullName,
        //                            success = true,
        //                            access_time = new DateTimeOffset(parentDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
        //                            modify_time = new DateTimeOffset(parentDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
        //                            size = 0,
        //                            files = files.ToList()
        //                        },
        //                    };
        //                }
        //            }
        //            else
        //            {
        //                return new FileBrowserResponseResult
        //                {
        //                    task_id = (string)args["task-id"],
        //                    completed = "true",
        //                    user_output = "done",
        //                    file_browser = new FileBrowser
        //                    {
        //                        host = Dns.GetHostName(),
        //                        is_file = true,
        //                        permissions = new Dictionary<string, string>(),
        //                        name = parentFileInfo.Name,
        //                        parent_path = Path.GetDirectoryName(parentFileInfo.FullName),
        //                        success = true,
        //                        access_time = new DateTimeOffset(parentFileInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
        //                        modify_time = new DateTimeOffset(parentFileInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
        //                        size = parentFileInfo.Length,
        //                        files = new List<FileBrowserFile>(),
        //                    },
        //                };
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            return new FileBrowserResponseResult
        //            {
        //                task_id = (string)args["task-id"],
        //                completed = "true",
        //                user_output = ex.ToString(),
        //                status = "error"
        //            };
        //        }
        //    }
        //    else
        //    {
        //        return new FileBrowserResponseResult
        //        {
        //            task_id = (string)args["task-id"],
        //            completed = "true",
        //            user_output = "No Path Specified",
        //        };
        //    }
        //}
    }
}
