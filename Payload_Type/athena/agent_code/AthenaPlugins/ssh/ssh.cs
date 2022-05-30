using PluginBase;
using Renci.SshNet;
using System.Text;

namespace Athena
{
    public static class ssh
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            string output = "";
            try
            {
                StringBuilder sb = new StringBuilder();
                ConnectionInfo connectionInfo;
                if (args.ContainsKey("key") && !String.IsNullOrEmpty((string)args["key"])) //SSH Key Auth
                {
                    string keyPath = (string)args["key"];
                    PrivateKeyAuthenticationMethod authenticationMethod;

                    if (!String.IsNullOrEmpty((string)args["passphrase"]))
                    {
                        PrivateKeyFile pk = new PrivateKeyFile(keyPath, (string)args["passphrase"]);
                        authenticationMethod = new PrivateKeyAuthenticationMethod((string)args["username"], new PrivateKeyFile[] { pk });
                    }
                    else
                    {
                        PrivateKeyFile pk = new PrivateKeyFile(keyPath);
                        authenticationMethod = new PrivateKeyAuthenticationMethod((string)args["username"], new PrivateKeyFile[] { pk });
                    }
                    output = RunCommand((string)args["command"], ((string)args["hosts"]).Split(',').ToList<string>(), (string)args["username"], authenticationMethod);

                }
                else //Username & Password Auth
                {
                    PasswordAuthenticationMethod authenticationMethod = new PasswordAuthenticationMethod((string)args["username"], (string)args["password"]);
                    output = RunCommand((string)args["command"], ((string)args["hosts"]).Split(',').ToList<string>(), (string)args["username"], authenticationMethod);
                }

                return new ResponseResult
                {
                    completed = "true",
                    user_output = output,
                    task_id = (string)args["task-id"], //task-id passed in from Athena
                };
            }
            catch (Exception ex)
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = ex.ToString(),
                    task_id = (string)args["task-id"], //task-id passed in from Athena
                    status = "error"
                };
            }
            
        }
        static string RunCommand(string command, List<string> hosts, string username, AuthenticationMethod authenticationMethod)
        {
            StringBuilder sb = new StringBuilder();
            Parallel.ForEach(hosts, host =>
            {
                ConnectionInfo connectionInfo = new ConnectionInfo(host, username, authenticationMethod);
                SshClient sshclient = new SshClient(connectionInfo);
                StringBuilder innerSb = new StringBuilder();
                sshclient.Connect();


                if (sshclient.IsConnected)
                {
                    if (string.IsNullOrEmpty(command))
                    { //just test connection
                        sb.AppendLine($"{host} - Connection Successful");
                    }
                    else //run command
                    {
                        SshCommand sc = sshclient.CreateCommand(command);
                        sc.Execute();
                        if (sc.ExitStatus != 0)
                        {
                            innerSb.AppendLine($"{host} - Exited with code: {sc.ExitStatus}");
                            innerSb.AppendLine(sc.Result);
                            innerSb.AppendLine(sc.Error);
                        }
                        else
                        {
                            innerSb.AppendLine($"{host} command ran successfully");
                            innerSb.AppendLine(sc.Result);
                        }
                        sb.AppendLine(innerSb.ToString());
                    }
                }
                else
                {
                    sb.AppendLine($"[{host}] - Connection Failed");
                }
            });
            return sb.ToString();
        }
    }

    

}
