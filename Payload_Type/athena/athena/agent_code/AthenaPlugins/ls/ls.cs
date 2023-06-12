using Athena.Commands;
using Athena.Commands.Models;
using Athena.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Athena.Models.Responses;

namespace Plugins
{
    public class Ls : AthenaPlugin
    {
        public override string Name => "ls";
        public override void Execute(Dictionary<string, string> args)
        {

            if (args["path"].Contains(":")) //If the path contains a colon, it's likely a windows path and not UNC
            {
                TaskResponseHandler.AddResponse(ReturnLocalListing(args["path"], args["task-id"]));
            }
            else //It could be a local *nix path or a remote UNC
            {
                if (args["host"].Equals(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase)) //If it's the same name as the current host
                {
                    TaskResponseHandler.AddResponse(ReturnLocalListing(args["path"], args["task-id"]));
                }
                else //UNC Host
                {
                    string fullPath = Path.Join(args["host"], args["path"]);
                    TaskResponseHandler.AddResponse(ReturnRemoteListing(fullPath, args["host"], args["task-id"]));
                }
            }
        }
        FileBrowserResponseResult ReturnRemoteListing(string path, string host, string taskid)
        {
            try
            {
                FileInfo baseFileInfo = new FileInfo(path);
                if (baseFileInfo.Attributes.HasFlag(FileAttributes.Directory)) //Check if they just requested info about a specific file or not
                {
                    DirectoryInfo baseDirectoryInfo = new DirectoryInfo(baseFileInfo.FullName);

                    if (baseDirectoryInfo.Parent is null) //Our requested directory has no parent
                    {
                        var files = GetFiles(path, host).ToList();
                        string output;
                        if (files.Count > 0)
                        {
                            output = $"Returned {files.Count} files in the file browser.";
                        }
                        else
                        {
                            output = $"No files returned.";
                        }



                        return new FileBrowserResponseResult
                        {
                            task_id = taskid,
                            completed = true,
                            user_output = output,
                            file_browser = new FileBrowser
                            {
                                host = host,
                                is_file = false,
                                permissions = new Dictionary<string, string>(),
                                name = baseDirectoryInfo.Name != "" ? NormalizeFileName(baseDirectoryInfo.Name, host) : NormalizeFileName(path, host).TrimStart('\\').TrimStart('/'),
                                parent_path = @"",
                                success = true,
                                access_time = Convert.ToUInt32(new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
                                modify_time = Convert.ToUInt32(new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
                                size = 0,
                                files = files,
                            },
                        };
                    }
                    else //Our requested directory has a parent
                    {
                        var files = GetFiles(path, host).ToList();
                        string output;
                        if (files.Count > 0)
                        {
                            output = $"Returned {files.Count} files in the file browser.";
                        }
                        else
                        {
                            output = $"No files returned.";
                        }
                        return new FileBrowserResponseResult
                        {
                            task_id = taskid,
                            completed = true,
                            user_output = output,
                            file_browser = new FileBrowser
                            {
                                host = host,
                                is_file = false,
                                permissions = new Dictionary<string, string>(),
                                name = NormalizeFileName(baseDirectoryInfo.Name, host),
                                parent_path = NormalizeFileName(baseDirectoryInfo.Parent.FullName, host).TrimStart('\\').TrimStart('/'),
                                success = true,
                                access_time = Convert.ToUInt32(new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
                                modify_time = Convert.ToUInt32(new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
                                size = 0,
                                files = files,
                            },
                        };
                    }
                }
                else //I don't think this will ever catch, but just in case
                {
                    return new FileBrowserResponseResult
                    {
                        task_id = taskid,
                        completed = true,
                        process_response = new Dictionary<string, string> { { "message", "0x28" } },
                        file_browser = new FileBrowser
                        {
                            host = host,
                            is_file = true,
                            permissions = new Dictionary<string, string>(),
                            name = baseFileInfo.Name,
                            parent_path = Path.GetDirectoryName(baseFileInfo.FullName),
                            success = true,
                            access_time = Convert.ToUInt32(new DateTimeOffset(baseFileInfo.LastAccessTime).ToUnixTimeMilliseconds()),
                            modify_time = Convert.ToUInt32(new DateTimeOffset(baseFileInfo.LastWriteTime).ToUnixTimeMilliseconds()),
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
                    completed = true,
                    user_output = ex.ToString(),
                    status = "error"
                };
            }
        }

        FileBrowserResponseResult ReturnLocalListing(string path, string taskid)
        {
            try
            {
                FileInfo baseFileInfo = new FileInfo(path);
                if (baseFileInfo.Attributes.HasFlag(FileAttributes.Directory)) //Check if they just requested info about a specific file or not
                {
                    DirectoryInfo baseDirectoryInfo = new DirectoryInfo(baseFileInfo.FullName);

                    if (baseDirectoryInfo.Parent is null) //Our requested directory has no parent
                    {
                        var files = GetFiles(path, "").ToList();
                        string output;
                        if (files.Count > 0)
                        {
                            output = $"0x28";
                        }
                        else
                        {
                            output = $"0x29.";
                        }
                        return new FileBrowserResponseResult
                        {
                            task_id = taskid,
                            completed = true,
                            process_response = new Dictionary<string, string> { { "message", output } },
                            file_browser = new FileBrowser
                            {
                                host = Dns.GetHostName(),
                                is_file = false,
                                permissions = new Dictionary<string, string>(),
                                name = baseDirectoryInfo.Name,
                                parent_path = "",
                                success = true,
                                access_time = Convert.ToUInt32(new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
                                modify_time = Convert.ToUInt32(new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
                                size = 0,
                                files = files,
                            },
                        };
                    }
                    else //Our requested directory has a parent
                    {
                        var files = GetFiles(path, "").ToList();
                        string output;
                        if (files.Count > 0)
                        {
                            output = $"0x28";
                        }
                        else
                        {
                            output = $"0x29";
                        }
                        return new FileBrowserResponseResult
                        {
                            task_id = taskid,
                            completed = true,
                            process_response = new Dictionary<string, string> { { "message", output } },
                            file_browser = new FileBrowser
                            {
                                host = Dns.GetHostName(),
                                is_file = false,
                                permissions = new Dictionary<string, string>(),
                                name = baseDirectoryInfo.Name,
                                parent_path = baseDirectoryInfo.Parent.FullName,
                                success = true,
                                access_time = Convert.ToUInt32(new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
                                modify_time = Convert.ToUInt32(new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
                                size = 0,
                                files = files,
                            },
                        };
                    }
                }
                else //I don't think this will ever catch, but just in case
                {
                    return new FileBrowserResponseResult
                    {
                        task_id = taskid,
                        completed = true,
                        process_response = new Dictionary<string, string> { { "message", "0x28" } },
                        file_browser = new FileBrowser
                        {
                            host = Dns.GetHostName(),
                            is_file = true,
                            permissions = new Dictionary<string, string>(),
                            name = baseFileInfo.Name,
                            parent_path = Path.GetDirectoryName(baseFileInfo.FullName),
                            success = true,
                            access_time = Convert.ToUInt32(new DateTimeOffset(baseFileInfo.LastAccessTime).ToUnixTimeMilliseconds()),
                            modify_time = Convert.ToUInt32(new DateTimeOffset(baseFileInfo.LastWriteTime).ToUnixTimeMilliseconds()),
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
                    completed = true,
                    user_output = ex.ToString(),
                    status = "error"
                };
            }
        }

        ConcurrentBag<FileBrowserFile> GetFiles(string path, string host)
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
                        var file = new FileBrowserFile
                        {
                            is_file = !fInfo.Attributes.HasFlag(FileAttributes.Directory),
                            permissions = new Dictionary<string, string>(),
                            name = NormalizeFileName(fInfo.Name, host),
                            access_time = Convert.ToUInt32(new DateTimeOffset(parentDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
                            modify_time = Convert.ToUInt32(new DateTimeOffset(parentDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
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
                        access_time = Convert.ToUInt32(new DateTimeOffset(parentFileInfo.LastAccessTime).ToUnixTimeMilliseconds()),
                        modify_time = Convert.ToUInt32(new DateTimeOffset(parentFileInfo.LastWriteTime).ToUnixTimeMilliseconds()),
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

        string NormalizeFileName(string path, string host)
        {

            if (host == "")
            {
                return path;
            }

            path = path.TrimStart('\\').TrimStart('\\').TrimEnd('/').TrimEnd('\\'); //Remove \\ at the beginning of the path and / at the end

            int index = path.IndexOf(host);
            string cleanPath = (index < 0)
                ? path
                : path.Remove(index, host.Length);

            return cleanPath;
        }
    }
}
