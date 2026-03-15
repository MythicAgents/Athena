using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using zip_inspect;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "zip-inspect";
        private IDataBroker messageManager { get; set; }
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            StringBuilder output = new StringBuilder();
            ZipInspectArgs args = JsonSerializer.Deserialize<ZipInspectArgs>(job.task.parameters);
            if (args is null){
                DebugLog.Log($"{Name} args null [{job.task.id}]");
                return;
            }
            FileInfo fInfo = new FileInfo(args.path);
            if (!fInfo.Exists)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = $"Zipfile does not exist: {args.path}",
                    task_id = job.task.id,
                });
                return;
            }

            if(args.path.EndsWith("zip", StringComparison.InvariantCultureIgnoreCase))
            {
                DebugLog.Log($"{Name} inspecting '{args.path}' [{job.task.id}]");
                extractZip(args.path, job.task.id);
            }
            else
            {
                DebugLog.Log($"{Name} unsupported format '{args.path}' [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = $"Only zip supported right now.",
                    task_id = job.task.id,
                });
            }
            //else if(args.path.EndsWith("gz", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    //extractGzip(args.path, job.task.id);
            //}

        }
        //void extractGzip(string path, string task_id)
        //{
        //    StringBuilder output = new StringBuilder();
        //    try
        //    {
        //        using (ZipArchive archive = ZipFile.OpenRead(path))
        //        {
        //            foreach (ZipArchiveEntry entry in archive.Entries)
        //            {
        //                output.AppendLine($"{entry.Length}\t {entry.FullName}");
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        messageManager.AddTaskResponse(new TaskResponse
        //        {
        //            completed = true,
        //            user_output = e.ToString(),
        //            task_id = task_id,
        //            status = "error"
        //        });
        //        return;
        //    }

        //    messageManager.AddTaskResponse(new TaskResponse
        //    {
        //        completed = true,
        //        user_output = FormatFileData(output.ToString()),
        //        task_id = task_id,
        //    });
        //}
        void extractZip(string path, string task_id)
        {
            StringBuilder output = new StringBuilder();
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        output.AppendLine($"{entry.Length}\t {entry.FullName}");
                    }
                }
            }
            catch (Exception e)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = e.ToString(),
                    task_id = task_id,
                    status = "error"
                });
                return;
            }

            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = FormatFileData(output.ToString()),
                task_id = task_id,
            });
        }
        private string FormatFileData(string data)
        {
            // Split the data into lines
            string[] lines = data.Trim().Split('\n');
            var structuredData = new List<(int Size, string Path)>();

            // Parse each line
            foreach (var line in lines)
            {
                // Split the line into size and path
                int firstSpaceIndex = line.IndexOf('\t');
                if (firstSpaceIndex == -1) continue;

                string sizePart = line.Substring(0, firstSpaceIndex).Trim();
                string pathPart = line.Substring(firstSpaceIndex).Trim();

                if (int.TryParse(sizePart, out int size))
                {
                    structuredData.Add((size, pathPart));
                }
            }

            // Determine column widths
            int maxSizeWidth = structuredData.Count > 0 ? structuredData.Max(entry => entry.Size.ToString().Length) : 0;
            int maxPathWidth = structuredData.Count > 0 ? structuredData.Max(entry => entry.Path.Length) : 0;

            // Create the formatted table
            var table = new List<string>
        {
            $"{"Size".PadLeft(maxSizeWidth)}  {"Path".PadRight(maxPathWidth)}",
            new string('-', maxSizeWidth + 2 + maxPathWidth)
        };

            foreach (var entry in structuredData)
            {
                string size = entry.Size.ToString().PadLeft(maxSizeWidth);
                string path = entry.Path.PadRight(maxPathWidth);
                table.Add($"{size}  {path}");
            }

            // Return the formatted table as a single string
            return string.Join(Environment.NewLine, table);
        }
    }
}
