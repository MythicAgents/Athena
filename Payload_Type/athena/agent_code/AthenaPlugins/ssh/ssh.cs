using PluginBase;
using Renci.SshNet;
using System.Text;

namespace Plugin
{
    public static class ssh
    {
        static SshClient sshClient;
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string action = (string)args["action"];
                switch (action.ToLower())
                {
                    case "exec":
                        return RunCommand(args);
                        break;
                    case "connect":
                        return Connect(args);
                        break;
                    case "disconnect":
                        return Disconnect(args);
                        break;
                }
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No valid command specified.",
                    completed = "true",
                    status = "error"
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

        static ResponseResult Connect(Dictionary<string, object> args)
        {
            ConnectionInfo connectionInfo;

            if (args.ContainsKey("key") && !String.IsNullOrEmpty((string)args["key"])) //SSH Key Auth
            {
                string keyPath = (string)args["key"];
                PrivateKeyAuthenticationMethod authenticationMethod;

                if (!String.IsNullOrEmpty((string)args["passphrase"]))
                {
                    PrivateKeyFile pk = new PrivateKeyFile(keyPath, (string)args["passphrase"]);
                    authenticationMethod = new PrivateKeyAuthenticationMethod((string)args["username"], new PrivateKeyFile[] { pk });
                    connectionInfo = new ConnectionInfo((string)args["host"], (string)args["username"], authenticationMethod);
                }
                else
                {
                    PrivateKeyFile pk = new PrivateKeyFile(keyPath);
                    authenticationMethod = new PrivateKeyAuthenticationMethod((string)args["username"], new PrivateKeyFile[] { pk });
                    connectionInfo = new ConnectionInfo((string)args["host"], (string)args["username"], authenticationMethod);
                }
            }
            else //Username & Password Auth
            {
                PasswordAuthenticationMethod authenticationMethod = new PasswordAuthenticationMethod((string)args["username"], (string)args["password"]);
                connectionInfo = new ConnectionInfo((string)args["host"], (string)args["username"], authenticationMethod);
            }
            sshClient = new SshClient(connectionInfo);

            try
            {
                sshClient.Connect();

                if (sshClient.IsConnected)
                {
                    return new ResponseResult
                    {
                        task_id = (string)args["task-id"],
                        user_output = $"Successfully connected to {(string)args["host"]}",
                        completed = "true",
                    };
                }
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"Failed to connect to {(string)args["host"]}",
                    completed = "true",
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"Failed to connect to {(string)args["host"]}{Environment.NewLine}{e.ToString()}",
                    completed = "true",
                };
            }

        }
        static ResponseResult Disconnect(Dictionary<string, object> args)
        {
            sshClient.Disconnect();

            if (!sshClient.IsConnected)
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"Disconnected.",
                    completed = "true",
                };
            }
            else
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"Failed to disconnect",
                    completed = "true",
                    status = "error",
                };
            }
        }
        static ResponseResult RunCommand(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();
            string command = (string)args["command"];

            if (!sshClient.IsConnected)
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No active connections. Please use connect to log into a host!",
                    completed = "true",
                    status = "error"
                };
            }

            if (string.IsNullOrEmpty(command))
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No command specified",
                    completed = "true",
                    status = "error"
                };
            }

            SshCommand sc = sshClient.CreateCommand(command);
            sc.Execute();

            if (sc.ExitStatus != 0)
            {
                sb.AppendLine(sc.Result);
                sb.AppendLine(sc.Error);
                sb.AppendLine($"Exited with code: {sc.ExitStatus}");
            }
            else
            {
                sb.AppendLine(sc.Result);
            }

            return new ResponseResult
            {
                user_output = sb.ToString(),
                completed = "true",
                task_id = (string)args["task-id"]
            };
        }
    }
}

  
