using KeepClip.Application.Paste;

namespace KeepClip.App.Interop;

internal sealed class WindowTarget : IWindowTarget
{
    public nint Capture()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        return IsValid(hwnd) ? hwnd : 0;
    }

    public bool IsValid(nint handle)
    {
        if (handle == 0 || !NativeMethods.IsWindow(handle))
        {
            return false;
        }

        if (handle == NativeMethods.GetShellWindow() || handle == NativeMethods.GetDesktopWindow())
        {
            return false;
        }

        var className = new System.Text.StringBuilder(256);
        NativeMethods.GetClassName(handle, className, className.Capacity);
        if (className.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow")
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(handle, out var pid);
        return pid != 0 && pid != Environment.ProcessId;
    }

    public async Task<bool> TryActivateAsync(nint handle, CancellationToken ct)
    {
        if (!IsValid(handle))
        {
            return false;
        }

        if (NativeMethods.IsIconic(handle))
        {
            NativeMethods.ShowWindow(handle, 9);
        }

        NativeMethods.SetForegroundWindow(handle);
        // Activation across input queues is asynchronous. Wait for the destination
        // to process the activation before checking focus and injecting Ctrl+V.
        ct.ThrowIfCancellationRequested();
        if (NativeMethods.SendMessageTimeout(handle, 0, 0, 0, 0x0002, 300, out _) == 0)
        {
            return false;
        }

        for (var i = 0; i < 10; i++)
        {
            if (IsForeground(handle))
            {
                return true;
            }

            await Task.Delay(20, ct);
        }

        return false;
    }

    public bool IsForeground(nint handle) => NativeMethods.GetForegroundWindow() == handle;
}
