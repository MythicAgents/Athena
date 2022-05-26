using PluginBase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Athena
{
    public static class Plugin
    {
        public static FileBrowserResponseResult Execute(Dictionary<string, object> args)
        {
            ConcurrentBag<FileBrowserFile> files = new ConcurrentBag<FileBrowserFile>();

            if (args.ContainsKey("path"))
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

                try
                {
                    FileInfo parentFileInfo = new FileInfo((string)args["path"]);
                    if (parentFileInfo.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        DirectoryInfo parentDirectoryInfo = new DirectoryInfo(parentFileInfo.FullName);

                        Parallel.ForEach(parentDirectoryInfo.GetFileSystemInfos(), (fInfo) =>
                        {
                            var file = new FileBrowserFile
                            {
                                is_file = !fInfo.Attributes.HasFlag(FileAttributes.Directory),
                                permissions = new Dictionary<string, string>(),
                                name = fInfo.Name,
                                access_time = fInfo.LastAccessTime.ToString("u", new CultureInfo("en-US Culture")),
                                modify_time = fInfo.LastWriteTime.ToString("u", new CultureInfo("en-US Culture"))
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
                        });

                        if(parentDirectoryInfo.Parent is null)
                        {
                            return new FileBrowserResponseResult
                            {
                                task_id = (string)args["task-id"],
                                completed = "true",
                                user_output = "done",
                                file_browser = new FileBrowser
                                {
                                    host = Dns.GetHostName(),
                                    is_file = false,
                                    permissions = new Dictionary<string, string>(),
                                    name = parentDirectoryInfo.Name,
                                    parent_path = "",
                                    success = true,
                                    access_time = parentDirectoryInfo.LastAccessTime.ToString("u", new CultureInfo("en-US Culture")),
                                    modify_time = parentDirectoryInfo.LastWriteTime.ToString("u", new CultureInfo("en-US Culture")),
                                    size = 0,
                                    files = files.ToList()
                                },
                            };
                        }
                        else
                        {
                            return new FileBrowserResponseResult
                            {
                                task_id = (string)args["task-id"],
                                completed = "true",
                                user_output = "done",
                                file_browser = new FileBrowser
                                {
                                    host = Dns.GetHostName(),
                                    is_file = false,
                                    permissions = new Dictionary<string, string>(),
                                    name = parentDirectoryInfo.Name,
                                    parent_path = parentDirectoryInfo.Parent.FullName,
                                    success = true,
                                    access_time = parentDirectoryInfo.LastAccessTime.ToString("u",new CultureInfo("en-US Culture")),
                                    modify_time = parentDirectoryInfo.LastWriteTime.ToString("u", new CultureInfo("en-US Culture")),
                                    size = 0,
                                    files = files.ToList()
                                },
                            };
                        }
                    }
                    else
                    {
                        return new FileBrowserResponseResult
                        {
                            task_id = (string)args["task-id"],
                            completed = "true",
                            user_output = "done",
                            file_browser = new FileBrowser
                            {
                                host = Dns.GetHostName(),
                                is_file = true,
                                permissions = new Dictionary<string, string>(),
                                name = parentFileInfo.Name,
                                parent_path = Path.GetDirectoryName(parentFileInfo.FullName),
                                success = true,
                                access_time = parentFileInfo.LastAccessTime.ToString("u", new CultureInfo("en-US Culture")),
                                modify_time = parentFileInfo.LastWriteTime.ToString("u", new CultureInfo("en-US Culture")),
                                size = parentFileInfo.Length,
                                files = new List<FileBrowserFile>(),
                            },
                        };
                    }
                }
                catch (Exception ex)
                {
                    return new FileBrowserResponseResult
                    {
                        task_id = (string)args["task-id"],
                        completed = "true",
                        user_output = ex.ToString(),
                        status = "error"
                    };
                }


            }
            else
            {
                return new FileBrowserResponseResult
                {
                    task_id = (string)args["task-id"],
                    completed = "true",
                    user_output = "No Path Specified",
                };
            }
        }


        public static ResponseResult ExecuteOld(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");

            try
            {
                string[] directories;
                if (args.ContainsKey("path"))
                {
                    directories = Directory.GetFileSystemEntries((string)args["path"]);
                }
                else
                {
                    directories = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory());
                }
                foreach (var dir in directories)
                {
                    sb.Append($"{{\"path\":\"{dir.Replace(@"\",@"\\")}\",\"LastAccessTime\":\"{Directory.GetLastAccessTime(dir)}\",\"LastWriteTime\":\"{Directory.GetLastWriteTime(dir)}\",\"CreationTime\":\"{Directory.GetCreationTime(dir)}\"}},");
                }

                sb.Remove(sb.Length - 1, 1);
                sb.Append("]");
                return new ResponseResult
                {
                    completed = "true",
                    user_output = sb.ToString(),
                    task_id = (string)args["task-id"],
                };

            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = e.Message,
                    task_id = (string)args["task-id"],
                };
            }
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
