using System.Runtime.InteropServices;

namespace KeepClip.App.Interop;

// Observe only button presses while the shelf is visible. Never consume input.
internal sealed class OutsideClickWatcher(nint window, Func<bool> watch, Action outside) : IDisposable
{
    private NativeMethods.MouseHookProc? _callback;
    private nint _hook;

    public void Start()
    {
        if (_hook != 0)
        {
            return;
        }

        _callback ??= OnMouse;
        _hook = NativeMethods.SetWindowsHookEx(14, _callback, NativeMethods.GetModuleHandle(null), 0);

    }

    private nint OnMouse(int code, nuint message, nint data)
    {
        try
        {
            if (code >= 0 && message is 0x0201 or 0x0204 or 0x0207 or 0x020B && watch())
            {
                var point = Marshal.PtrToStructure<NativeMethods.MouseHookData>(data).Point;
                if (NativeMethods.GetWindowRect(window, out var rect) && (point.X < rect.Left || point.X >= rect.Right || point.Y < rect.Top || point.Y >= rect.Bottom))
                {
                    outside();
                }
            }
        }
        catch
        { /* Exceptions must not cross the native hook boundary. */
        }

        return NativeMethods.CallNextHookEx(_hook, code, message, data);
    }

    public void Stop()
    {
        if (_hook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        GC.KeepAlive(_callback);
    }

    public void Dispose() => Stop();
}
