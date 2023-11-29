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
using System.Globalization;
using LsUtilities;

namespace Plugins
{
    public class Ls : AthenaPlugin
    {
        public override string Name => "ls";
        public override void Execute(Dictionary<string, string> args)
        {
            if (args["path"].Contains(":")) //If the path contains a colon, it's likely a windows path and not UNC
            {
                if (args["path"].Split('\\').Count() == 1) //It's a root dir and didn't include a \
                {
                    args["path"] = args["path"] + "\\";
                }

                TaskResponseHandler.AddResponse(LocalListing.GetLocalListing(args["path"], args["task-id"]));

                //TaskResponseHandler.AddResponse(ReturnLocalListing(args["path"], args["task-id"]));
            }
            else //It could be a local *nix path or a remote UNC
            {
                if (args["host"].Equals(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase)) //If it's the same name as the current host
                {
                    Console.WriteLine("Host is the same as our DNS name");
                    TaskResponseHandler.AddResponse(LocalListing.GetLocalListing(args["path"], args["task-id"]));
                }
                else //UNC Host
                {
                    Console.WriteLine("Getting remote host");
                    string fullPath = Path.Join(args["host"], args["path"]);
                    string host = args["host"];
                    if (host == "" && args["path"].StartsWith("\\\\"))
                    {
                        host = new Uri(args["path"]).Host;
                    }
                    else
                    {
                        fullPath = Path.Join("\\\\" + host, args["path"]);
                    }
                    TaskResponseHandler.AddResponse(RemoteListing.GetRemoteListing(fullPath, host, args["task-id"]));
                }
            }
        }
        //FileBrowserResponseResult ReturnRemoteListing(string path, string host, string taskid)
        //{
        //    try
        //    {
        //        FileInfo baseFileInfo = new FileInfo(path);
        //        if (baseFileInfo.Attributes.HasFlag(FileAttributes.Directory)) //Check if they just requested info about a specific file or not
        //        {
        //            DirectoryInfo baseDirectoryInfo = new DirectoryInfo(baseFileInfo.FullName);

        //            if (baseDirectoryInfo.Parent is null) //Our requested directory has no parent
        //            {
        //                var files = GetFiles(path, host).ToList();
        //                string output;
        //                if (files.Count > 0)
        //                {
        //                    output = $"Returned {files.Count} files in the file browser.";
        //                }
        //                else
        //                {
        //                    output = $"No files returned.";
        //                }



        //                return new FileBrowserResponseResult
        //                {
        //                    task_id = taskid,
        //                    completed = true,
        //                    user_output = output,
        //                    file_browser = new FileBrowser
        //                    {
        //                        host = host,
        //                        is_file = false,
        //                        permissions = new Dictionary<string, string>(),
        //                        name = baseDirectoryInfo.Name != "" ? NormalizeFileName(baseDirectoryInfo.Name, host) : NormalizeFileName(path, host).TrimStart('\\').TrimStart('/'),
        //                        parent_path = @"",
        //                        success = true,
        //                        access_time = GetTimeStamp(new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
        //                        modify_time = GetTimeStamp(new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
        //                        size = 0,
        //                        files = files,
        //                    },
        //                };
        //            }
        //            else //Our requested directory has a parent
        //            {
        //                var files = GetFiles(path, host).ToList();
        //                string output;
        //                if (files.Count > 0)
        //                {
        //                    output = $"Returned {files.Count} files in the file browser.";
        //                }
        //                else
        //                {
        //                    output = $"No files returned.";
        //                }

        //                return new FileBrowserResponseResult
        //                {
        //                    task_id = taskid,
        //                    completed = true,
        //                    process_response = new Dictionary<string, string> { { "message", "0x28" } },
        //                    file_browser = new FileBrowser
        //                    {
        //                        host = host,
        //                        is_file = false,
        //                        permissions = new Dictionary<string, string>(),
        //                        name = NormalizeFileName(baseDirectoryInfo.Name, host),
        //                        parent_path = NormalizeFileName(baseDirectoryInfo.Parent.FullName, host).TrimStart('\\').TrimStart('/'),
        //                        success = true,
        //                        access_time = GetTimeStamp(new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
        //                        modify_time = GetTimeStamp(new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
        //                        size = 0,
        //                        files = files,
        //                    },
        //                };
        //            }
        //        }
        //        else //I don't think this will ever catch, but just in case
        //        {
        //            return new FileBrowserResponseResult
        //            {
        //                task_id = taskid,
        //                completed = true,
        //                process_response = new Dictionary<string, string> { { "message", "0x28" } },
        //                file_browser = new FileBrowser
        //                {
        //                    host = host,
        //                    is_file = true,
        //                    permissions = new Dictionary<string, string>(),
        //                    name = baseFileInfo.Name,
        //                    parent_path = Path.GetDirectoryName(baseFileInfo.FullName),
        //                    success = true,
        //                    access_time = GetTimeStamp(new DateTimeOffset(baseFileInfo.LastAccessTime).ToUnixTimeMilliseconds()),
        //                    modify_time = GetTimeStamp(new DateTimeOffset(baseFileInfo.LastWriteTime).ToUnixTimeMilliseconds()),
        //                    size = baseFileInfo.Length,
        //                    files = new List<FileBrowserFile>(),
        //                },
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return new FileBrowserResponseResult
        //        {
        //            task_id = taskid,
        //            completed = true,
        //            user_output = ex.ToString(),
        //            status = "error"
        //        };
        //    }
        //}

        //FileBrowserResponseResult ReturnLocalListing(string path, string taskid)
        //{
        //    if (path == ".")
        //    {
        //        path = Directory.GetCurrentDirectory();
        //    }

        //    try
        //    {
        //        FileInfo baseFileInfo = new FileInfo(path);
        //        if (baseFileInfo.Attributes.HasFlag(FileAttributes.Directory)) //Check if they just requested info about a specific file or not
        //        {
        //            DirectoryInfo baseDirectoryInfo = new DirectoryInfo(baseFileInfo.FullName);

        //            if (baseDirectoryInfo.Parent is null) //Our requested directory has no parent
        //            {
        //                var files = GetFiles(path, "").ToList();
        //                string output;
        //                if (files.Count > 0)
        //                {
        //                    output = $"0x28";
        //                }
        //                else
        //                {
        //                    output = $"0x29";
        //                }
        //                return new FileBrowserResponseResult
        //                {
        //                    task_id = taskid,
        //                    completed = true,
        //                    process_response = new Dictionary<string, string> { { "message", output } },
        //                    file_browser = new FileBrowser
        //                    {
        //                        host = Dns.GetHostName(),
        //                        is_file = false,
        //                        permissions = new Dictionary<string, string>(),
        //                        name = baseDirectoryInfo.Name,
        //                        parent_path = "",
        //                        success = true,
        //                        access_time = GetTimeStamp(new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
        //                        modify_time = GetTimeStamp(new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
        //                        size = 0,
        //                        files = files,
        //                    },
        //                };
        //            }
        //            else //Our requested directory has a parent
        //            {
        //                var files = GetFiles(path, "").ToList();
        //                string output;
        //                if (files.Count > 0)
        //                {
        //                    output = $"0x28";
        //                }
        //                else
        //                {
        //                    output = $"0x29";
        //                }
        //                return new FileBrowserResponseResult
        //                {
        //                    task_id = taskid,
        //                    completed = true,
        //                    process_response = new Dictionary<string, string> { { "message", output } },
        //                    file_browser = new FileBrowser
        //                    {
        //                        host = Dns.GetHostName(),
        //                        is_file = false,
        //                        permissions = new Dictionary<string, string>(),
        //                        name = baseDirectoryInfo.Name,
        //                        parent_path = baseDirectoryInfo.Parent.FullName,
        //                        success = true,
        //                        access_time = GetTimeStamp(new DateTimeOffset(baseDirectoryInfo.LastAccessTime).ToUnixTimeMilliseconds()),
        //                        modify_time = GetTimeStamp(new DateTimeOffset(baseDirectoryInfo.LastWriteTime).ToUnixTimeMilliseconds()),
        //                        size = 0,
        //                        files = files,
        //                    },
        //                };
        //            }
        //        }
        //        else //I don't think this will ever catch, but just in case
        //        {
        //            return new FileBrowserResponseResult
        //            {
        //                task_id = taskid,
        //                completed = true,
        //                process_response = new Dictionary<string, string> { { "message", "0x28" } },
        //                file_browser = new FileBrowser
        //                {
        //                    host = Dns.GetHostName(),
        //                    is_file = true,
        //                    permissions = new Dictionary<string, string>(),
        //                    name = baseFileInfo.Name,
        //                    parent_path = Path.GetDirectoryName(baseFileInfo.FullName),
        //                    success = true,
        //                    access_time = GetTimeStamp(new DateTimeOffset(baseFileInfo.LastAccessTime).ToUnixTimeMilliseconds()),
        //                    modify_time = GetTimeStamp(new DateTimeOffset(baseFileInfo.LastWriteTime).ToUnixTimeMilliseconds()),
        //                    size = baseFileInfo.Length,
        //                    files = new List<FileBrowserFile>(),
        //                },
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return new FileBrowserResponseResult
        //        {
        //            task_id = taskid,
        //            completed = true,
        //            user_output = ex.ToString(),
        //            status = "error"
        //        };
        //    }
        //}



        string NormalizeFileName(string path, string host)
        {
            if (OperatingSystem.IsWindows()) //If we're on a windows OS replace / with \ so that I can parse it easier.
            {
                string newPath = String.Empty;
                if(TryGetExactPath(path, out newPath))
                {
                    path = newPath;
                }
            }
            else
            {
                path = path.Replace(@"\", @"/");
            }


            if (host == "")
            {
                return path;
            }
            try
            {
                return StripPathOfHost(path);
            }
            catch
            {
                return path;
            }
        }

        UInt64 GetTimeStamp(long timestamp)
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

        /// <summary>
        /// Gets the exact case used on the file system for an existing file or directory.
        /// </summary>
        /// <param name="path">A relative or absolute path.</param>
        /// <param name="exactPath">The full path using the correct case if the path exists.  Otherwise, null.</param>
        /// <returns>True if the exact path was found.  False otherwise.</returns>
        /// <remarks>
        /// This supports drive-lettered paths and UNC paths, but a UNC root
        /// will be returned in title case (e.g., \\Server\Share).
        /// </remarks>
        private bool TryGetExactPath(string path, out string exactPath)
        {
            bool result = false;
            exactPath = null;

            // DirectoryInfo accepts either a file path or a directory path, and most of its properties work for either.
            // However, its Exists property only works for a directory path.
            DirectoryInfo directory = new DirectoryInfo(path);
            if (File.Exists(path) || directory.Exists)
            {
                List<string> parts = new List<string>();

                DirectoryInfo parentDirectory = directory.Parent;
                while (parentDirectory != null)
                {
                    FileSystemInfo entry = parentDirectory.EnumerateFileSystemInfos(directory.Name).First();
                    parts.Add(entry.Name);

                    directory = parentDirectory;
                    parentDirectory = directory.Parent;
                }

                // Handle the root part (i.e., drive letter or UNC \\server\share).
                string root = directory.FullName;
                if (root.Contains(':'))
                {
                    root = root.ToUpper();
                }
                else
                {
                    string[] rootParts = root.Split('\\');
                    root = string.Join("\\", rootParts.Select(part => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(part)));
                }

                parts.Add(root);
                parts.Reverse();
                exactPath = Path.Combine(parts.ToArray());
                result = true;
            }

            return result;
        }


        private string StripPathOfHost(string path)
        {
            if (path.StartsWith(@"\\"))
            {
                return new string(path.Skip(path.IndexOf('\\', 2) + 1).ToArray());
            }
            return path;
        }
    }
}
