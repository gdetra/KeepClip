using KeepClip.Application.Paste;

namespace KeepClip.App.Interop;

internal sealed class ForegroundTracker : IDisposable
{
    private readonly ForegroundHistory _history = new(new WindowTarget().IsValid);
    private readonly NativeMethods.WinEventProc _callback;
    private readonly nint _hook;

    public ForegroundTracker()
    {
        _history.Observe(NativeMethods.GetForegroundWindow());
        _callback = (_, _, window, _, _, _, _) => _history.Observe(window);
        _hook = NativeMethods.SetWinEventHook(3, 3, 0, _callback, 0, 0, 2);

    }

    public nint Capture() => _history.Resolve(NativeMethods.GetForegroundWindow());

    public void Dispose()
    {
        if (_hook != 0)
        {
            NativeMethods.UnhookWinEvent(_hook);
        }

        GC.KeepAlive(_callback);
    }
}
