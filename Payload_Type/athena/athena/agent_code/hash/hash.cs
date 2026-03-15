using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Security.Cryptography;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "hash";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<hash.HashArgs>(job.task.parameters);

            if (args is null)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Failed to deserialize arguments.",
                    task_id = job.task.id,
                    status = "error"
                });
                return;
            }

            try
            {
                string result = args.action switch
                {
                    "hash" => ComputeHash(args),
                    "base64" => ProcessBase64(args),
                    _ => throw new ArgumentException($"Unknown action: {args.action}")
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id,
                });
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private string ComputeHash(hash.HashArgs args)
        {
            if (!File.Exists(args.path))
                throw new FileNotFoundException($"File not found: {args.path}");

            using var stream = File.OpenRead(args.path);
            byte[] hashBytes = args.algorithm.ToLowerInvariant() switch
            {
                "md5" => MD5.HashData(stream),
                "sha1" => SHA1.HashData(stream),
                "sha256" => SHA256.HashData(stream),
                "sha384" => SHA384.HashData(stream),
                "sha512" => SHA512.HashData(stream),
                _ => throw new ArgumentException($"Unsupported algorithm: {args.algorithm}")
            };

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private string ProcessBase64(hash.HashArgs args)
        {
            if (!File.Exists(args.path))
                throw new FileNotFoundException($"File not found: {args.path}");

            if (args.encode)
            {
                byte[] fileBytes = File.ReadAllBytes(args.path);
                return Convert.ToBase64String(fileBytes);
            }
            else
            {
                string content = File.ReadAllText(args.path).Trim();
                byte[] decoded = Convert.FromBase64String(content);
                return System.Text.Encoding.UTF8.GetString(decoded);
            }
        }
    }
}
