using System;
using System.Collections.Generic;
using Workflow.Contracts;

namespace Workflow
{
    class Config
    {
        public List<string> folders = new List<string>();
        public string targetPath;
        public string targetIcon;
        public string targetFilename;
        public string targetLocation;
        public string task_id = "";
        public bool timestomp = false;
        private IDataBroker messageManager;

        public Config(IDataBroker messageManager)
        {
            this.messageManager = messageManager;
        }

        public void WalkDirectoryTree(string root)
        {
            Stack<string> dirs = new Stack<string>();
            folders.Clear();

            messageManager.Write(
                "[*] Walking directory tree for: " + root,
                task_id, false);

            if (!System.IO.Directory.Exists(root))
            {
                messageManager.Write(
                    "[!] Error, folder does not exist",
                    task_id, true, "error");
                return;
            }
            dirs.Push(root);
            folders.Add(root);

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Pop();
                string[] subDirs;
                try
                {
                    subDirs = System.IO.Directory.GetDirectories(currentDir);
                }
                catch (UnauthorizedAccessException e)
                {
                    messageManager.Write(e.ToString(), task_id, false);
                    continue;
                }
                catch (System.IO.DirectoryNotFoundException e)
                {
                    messageManager.Write(e.ToString(), task_id, false);
                    continue;
                }

                foreach (string str in subDirs)
                {
                    dirs.Push(str);
                    folders.Add(str);
                }
            }
        }

        public void ApplyTimestomp(string filePath)
        {
            if (!timestomp)
                return;

            try
            {
                string dir = System.IO.Path.GetDirectoryName(filePath);
                var files = System.IO.Directory.GetFiles(dir);
                string neighbor = null;
                foreach (var f in files)
                {
                    if (!f.Equals(filePath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        neighbor = f;
                        break;
                    }
                }

                if (neighbor == null)
                    return;

                System.IO.File.SetCreationTime(
                    filePath,
                    System.IO.File.GetCreationTime(neighbor));
                System.IO.File.SetLastWriteTime(
                    filePath,
                    System.IO.File.GetLastWriteTime(neighbor));
                System.IO.File.SetLastAccessTime(
                    filePath,
                    System.IO.File.GetLastAccessTime(neighbor));
            }
            catch
            {
                // Timestomping is best-effort
            }
        }
    }
}
