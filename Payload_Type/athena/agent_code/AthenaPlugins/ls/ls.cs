using PluginBase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Plugin
{
    public static class ls
    {
        public static FileBrowserResponseResult Execute(Dictionary<string, object> args)
        {
            if (args["path"] is not null)
            {
                string path = ((string)args["path"]).Replace("\"", "");
                string host;
                if (args.ContainsKey("host"))
                {
                    host = args["host"].ToString();
                }
                else
                {
                    host = "";
                }

                if (!String.IsNullOrEmpty(host))
                {
                    //path = @"\\" + host + @"\" + path;
                    if (!(path.EndsWith(@"\") || path.EndsWith("/")))
                    {
                        if (path.Contains(@"\\")) //This will break with files, but we don't support ls'ing files directly anyways
                        {
                            path += @"\";
                        }
                        else
                        {
                            path += "/";
                        }
                    }
                    
                    string tempPath = @"\\" + host + @"\" + path;
                    if (!File.Exists(tempPath) && !Directory.Exists(tempPath))
                    {
                        return new FileBrowserResponseResult
                        {
                            user_output = $"File/Folder not found: {path}",
                            completed = "true",
                            status = "error",
                            task_id = (string)args["task-id"]
                        };
                    }
                    //Get Remote Files
                    return ReturnRemoteListing(tempPath, host, (string)args["task-id"]);
                }
                else
                {
                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        return new FileBrowserResponseResult
                        {
                            user_output = $"File/Folder not found: {path}",
                            completed = "true",
                            status = "error",
                            task_id = (string)args["task-id"]
                        };
                    }
                    return ReturnLocalListing(path, (string)args["task-id"]);
                    //Get Local Files
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
        
        static FileBrowserResponseResult ReturnRemoteListing(string path,string host, string taskid)
        {
            try
            {
                FileInfo baseFileInfo = new FileInfo(path);
                if (baseFileInfo.Attributes.HasFlag(FileAttributes.Directory)) //Check if they just requested info about a specific file or not
                {
                    DirectoryInfo baseDirectoryInfo = new DirectoryInfo(baseFileInfo.FullName);
                    
                    if (baseDirectoryInfo.Parent is null) //Our requested directory has no parent
                    {
                        return new FileBrowserResponseResult
                        {
                            task_id = taskid,
                            completed = "true",
                            user_output = "done",
                            file_browser = new FileBrowser
                            {
                                host = host,
                                is_file = false,
                                permissions = new Dictionary<string, string>(),
                                name = baseDirectoryInfo.Name != "" ? NormalizeFileName(baseDirectoryInfo.Name, host) : NormalizeFileName(path, host).TrimStart('\\').TrimStart('/'),
                                parent_path = @"",
                                success = true,
                                access_time = new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                                modify_time = new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
                                size = 0,
                                files = GetFiles(path, host).ToList(),
                            },
                        };
                    }
                    else //Our requested directory has a parent
                    {
                        Console.WriteLine("Base parent is not null.");
                        return new FileBrowserResponseResult
                        {
                            task_id = taskid,
                            completed = "true",
                            user_output = "done",
                            file_browser = new FileBrowser
                            {
                                host = host,
                                is_file = false,
                                permissions = new Dictionary<string, string>(),
                                name = NormalizeFileName(baseDirectoryInfo.Name, host),
                                parent_path = NormalizeFileName(baseDirectoryInfo.Parent.FullName, host).TrimStart('\\').TrimStart('/'),
                                success = true,
                                access_time = new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                                modify_time = new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
                                size = 0,
                                files = GetFiles(path, host).ToList(),
                            },
                        };
                    }
                }
                else //I don't think this will ever catch, but just in case
                {
                    return new FileBrowserResponseResult
                    {
                        task_id = taskid,
                        completed = "true",
                        user_output = "done",
                        file_browser = new FileBrowser
                        {
                            host = host,
                            is_file = true,
                            permissions = new Dictionary<string, string>(),
                            name = baseFileInfo.Name,
                            parent_path = Path.GetDirectoryName(baseFileInfo.FullName),
                            success = true,
                            access_time = new DateTimeOffset(baseFileInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                            modify_time = new DateTimeOffset(baseFileInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
                            size = baseFileInfo.Length,
                            files = new List<FileBrowserFile>(),
                        },
                    };
                }
            }
            catch (Exception ex)
            {
                return new FileBrowserResponseResult
                {
                    task_id = taskid,
                    completed = "true",
                    user_output = ex.ToString(),
                    status = "error"
                };
            }
        }

        static FileBrowserResponseResult ReturnLocalListing(string path, string taskid)
        {
            try
            {
                FileInfo baseFileInfo = new FileInfo(path);
                if (baseFileInfo.Attributes.HasFlag(FileAttributes.Directory)) //Check if they just requested info about a specific file or not
                {
                    DirectoryInfo baseDirectoryInfo = new DirectoryInfo(baseFileInfo.FullName);

                    if (baseDirectoryInfo.Parent is null) //Our requested directory has no parent
                    {
                        return new FileBrowserResponseResult
                        {
                            task_id = taskid,
                            completed = "true",
                            user_output = "done",
                            file_browser = new FileBrowser
                            {
                                host = Dns.GetHostName(),
                                is_file = false,
                                permissions = new Dictionary<string, string>(),
                                name = baseDirectoryInfo.Name,
                                parent_path = "",
                                success = true,
                                access_time = new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                                modify_time = new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
                                size = 0,
                                files = GetFiles(path, "").ToList(),
                            },
                        };
                    }
                    else //Our requested directory has a parent
                    {
                        return new FileBrowserResponseResult
                        {
                            task_id = taskid,
                            completed = "true",
                            user_output = "done",
                            file_browser = new FileBrowser
                            {
                                host = Dns.GetHostName(),
                                is_file = false,
                                permissions = new Dictionary<string, string>(),
                                name = baseDirectoryInfo.Name,
                                parent_path = baseDirectoryInfo.Parent.FullName,
                                success = true,
                                access_time = new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                                modify_time = new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
                                size = 0,
                                files = GetFiles(path, "").ToList(),
                            },
                        };
                    }
                }
                else //I don't think this will ever catch, but just in case
                {
                    return new FileBrowserResponseResult
                    {
                        task_id = taskid,
                        completed = "true",
                        user_output = "done",
                        file_browser = new FileBrowser
                        {
                            host = Dns.GetHostName(),
                            is_file = true,
                            permissions = new Dictionary<string, string>(),
                            name = baseFileInfo.Name,
                            parent_path = Path.GetDirectoryName(baseFileInfo.FullName),
                            success = true,
                            access_time = new DateTimeOffset(baseFileInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                            modify_time = new DateTimeOffset(baseFileInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
                            size = baseFileInfo.Length,
                            files = new List<FileBrowserFile>(),
                        },
                    };
                }
            }
            catch (Exception ex)
            {
                return new FileBrowserResponseResult
                {
                    task_id = taskid,
                    completed = "true",
                    user_output = ex.ToString(),
                    status = "error"
                };
             }
        }

        static ConcurrentBag<FileBrowserFile> GetFiles(string path, string host)
        {
            ConcurrentBag<FileBrowserFile> files = new ConcurrentBag<FileBrowserFile>();
            try
            {
                FileInfo parentFileInfo = new FileInfo(path);
                if (parentFileInfo.Attributes.HasFlag(FileAttributes.Directory))
                {
                    DirectoryInfo parentDirectoryInfo = new DirectoryInfo(parentFileInfo.FullName);
                    

                    Parallel.ForEach(parentDirectoryInfo.GetFileSystemInfos(), fInfo =>
                    {
                        Console.WriteLine(NormalizeFileName(fInfo.Name, host));
                        var file = new FileBrowserFile
                        {
                            is_file = !fInfo.Attributes.HasFlag(FileAttributes.Directory),
                            permissions = new Dictionary<string, string>(),
                            name = NormalizeFileName(fInfo.Name, host),
                            access_time = new DateTimeOffset(parentDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                            modify_time = new DateTimeOffset(parentDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
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

                }
                else
                {
                    files.Add(new FileBrowserFile()
                    {
                        is_file = !parentFileInfo.Attributes.HasFlag(FileAttributes.Directory),
                        permissions = new Dictionary<string, string>(),
                        name = parentFileInfo.Name,
                        access_time = new DateTimeOffset(parentFileInfo.LastAccessTime).ToUnixTimeMilliseconds().ToString(),
                        modify_time = new DateTimeOffset(parentFileInfo.LastWriteTime).ToUnixTimeMilliseconds().ToString(),
                        size = parentFileInfo.Length,
                    });
                }
                return files;
            }
            catch (Exception e)
            {
                return new ConcurrentBag<FileBrowserFile>();
            }
        }

        static string NormalizeFileName(string path, string host)
        {

            if(host == "")
            {
                return path;
            }
            
            path = path.TrimStart('\\').TrimStart('\\'); //Remove \\ at the beginning of the path

            int index = path.IndexOf(host);
            string cleanPath = (index < 0)
                ? path
                : path.Remove(index, host.Length);

            return cleanPath;
        }
    }
}
