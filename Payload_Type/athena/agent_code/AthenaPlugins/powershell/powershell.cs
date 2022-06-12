using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

namespace Athena
{
    public static class Plugin
    {
        
        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            bool isSuccess = false;
            string resStr;

            if (args.ContainsKey("psh_command") || args.ContainsKey("psh_file"))
            {
                using (PowerShell ps = PowerShell.Create())
                {
                    if (args.ContainsKey("psh_command") && (string)args["psh_command"]!="")
                    {
                        ps.AddScript((string)args["psh_command"]);
                    }
                    if (args.ContainsKey("psh_file") && (string)args["psh_file"] != "")
                    {
                        var base64EncodedBytes = Convert.FromBase64String((string)args["ps1"]);
                        var psStr=Encoding.UTF8.GetString(base64EncodedBytes);
                        ps.AddScript((string)psStr);
                    }

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
                                            sb.AppendLine(x.ToString());
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
        return new PluginResponse()
        {
              success = isSuccess,
              output = resStr
        };
    }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }

}
