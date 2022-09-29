using Athena.Plugins;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "newami";
        //public new void Execute(Dictionary<string, object> args)
        //{
        //    PluginHandler.AddResponse(new ResponseResult()
        //    {
        //        task_id = (string)args["task-id"],
        //        user_output = $"{Environment.UserDomainName}\\{Environment.UserName}",
        //        completed = "true"
        //    });
        //}

        public override void Execute(Dictionary<string, object> args)
        {
            Console.WriteLine($"{Environment.UserDomainName}\\{Environment.UserName}");
            PluginHandler.AddResponse(new ResponseResult()
            {
                task_id = (string)args["task-id"],
                user_output = $"{Environment.UserDomainName}\\{Environment.UserName}",
                completed = "true"
            });
        }
    }
}