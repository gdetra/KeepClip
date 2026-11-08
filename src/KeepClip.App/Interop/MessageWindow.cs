using System.ComponentModel;

namespace KeepClip.App.Interop;

internal sealed class MessageWindow : IDisposable
{
    private readonly NativeMethods.WindowProc _proc;
    private readonly string _className = "KeepClip.Messages." + Guid.NewGuid().ToString("N");
    private readonly nint _instance = NativeMethods.GetModuleHandle(null);

    public nint Handle { get; }

    public event Action<uint, nuint, nint>? Message;

    public MessageWindow()
    {
        _proc = WndProc;
        var cls = new NativeMethods.WindowClass
        {
            Proc = _proc,
            ClassName = _className,
            Instance = _instance
        };
        if (NativeMethods.RegisterClass(ref cls) == 0)
        {
            throw new Win32Exception();
        }

        Handle = NativeMethods.CreateWindowEx(0, _className, "KeepClip messages", 0x80000000, 0, 0, 0, 0, 0, 0, _instance, 0);
        if (Handle == 0)
        {
            throw new Win32Exception();
        }
    }

    private nint WndProc(nint hwnd, uint msg, nuint wp, nint lp)
    {
        // Exceptions may never cross the unmanaged callback boundary.
        try
        {
            Message?.Invoke(msg, wp, lp);
        }
        catch (Exception)
        {
        }

        return NativeMethods.DefWindowProc(hwnd, msg, wp, lp);
    }

    public void Dispose()
    {
        NativeMethods.DestroyWindow(Handle);
        NativeMethods.UnregisterClass(_className, _instance);
        GC.KeepAlive(_proc);
    }
}
