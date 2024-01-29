using Agent.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Agent
{
    public class ChromeFinder : IFinder
    {
        public string FindPath()
        {
            List<string> searchPaths;
            if (OperatingSystem.IsWindows())
            {
                searchPaths = new List<string>()
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe")
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                searchPaths = new List<string>()
                {
                    Path.Combine("Applications","Google Chrome.app","Contents","MacOS","Google Chrome")
                };
            }
            else if (OperatingSystem.IsLinux())
            {
                searchPaths = new List<string>()
                {
                    Path.Combine("opt","google","chrome","google-chrome"),
                    Path.Combine("usr","bin","google-chrome")
                };
            }
            else
            {
                return String.Empty;
            }
            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return String.Empty;
        }
    }
}
