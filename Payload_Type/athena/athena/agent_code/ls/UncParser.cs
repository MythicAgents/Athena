using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LsUtilities
{
    public class UNCPathParser
    {
        public string FullPath { get; set; }

        public UNCPathParser(string uncPath)
        {
            this.FullPath = uncPath.TrimEnd('\\');
        }

        public string GetParentPath(bool includeServer = false)
        {
            // Check if the path is valid UNC path
            if (!IsUNCPathValid())
            {
                throw new ArgumentException("Invalid UNC path.");
            }

            // Remove the server part and extract the parent path
            int lastSeparatorIndex = FullPath.LastIndexOf('\\');
            if (lastSeparatorIndex > 1)
            {
                if (includeServer)
                {
                    return FullPath.Substring(0, lastSeparatorIndex + 1); // Include the separator
                }
                else
                {
                    int serverEndIndex = FullPath.IndexOf('\\', 2); // Start searching after the first two backslashes
                    if (serverEndIndex != -1 && serverEndIndex < FullPath.Length - 1)
                    {
                        return FullPath.Substring(serverEndIndex + 1, lastSeparatorIndex - serverEndIndex).TrimEnd('\\');
                    }
                    else
                    {
                        return FullPath.Substring(0, lastSeparatorIndex).TrimEnd('\\');
                    }
                }
            }
            else
            {
                return string.Empty; // No parent path, root directory
            }
        }

        public string GetFileName()
        {
            // Check if the path is valid UNC path
            if (!IsUNCPathValid())
            {
                throw new ArgumentException("Invalid UNC path.");
            }

            // Extract the file or directory name
            int lastSeparatorIndex = FullPath.LastIndexOf('\\');
            if (lastSeparatorIndex != -1 && lastSeparatorIndex < FullPath.Length - 1)
            {
                return FullPath.Substring(lastSeparatorIndex + 1);
            }
            else
            {
                return FullPath; // Path is the root directory itself
            }
        }

        private bool IsUNCPathValid()
        {
            // A basic check to determine if the provided path looks like a UNC path
            return FullPath.StartsWith(@"\\") && FullPath.Length > 2;
        }
    }
}
