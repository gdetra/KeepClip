using System.Runtime.InteropServices;
using KeepClip.App.Interop;

namespace KeepClip.App.Desktop;

internal sealed class WindowsVHotkey : IDisposable
{
    private const int LowLevelKeyboardHook = 13;
    private const int InvalidHookHandle = 1404;
    private const int V = 0x56, LeftWindows = 0x5B, RightWindows = 0x5C;
    private const int Shift = 0x10, Control = 0x11, Alt = 0x12;
    private const uint Injected = 0x10;
    private static readonly int _inputSize = Marshal.SizeOf<NativeMethods.Input>();
    // A dummy key-up keeps Start from opening when the shell sees Win released
    // after we consume V. This is also used by the PowerToys centralized hook.
    private static readonly NativeMethods.Input[] _startMenuMask = [new()
    {
        Type = 1,
        Data = new()
        {
            Keyboard = new()
            {
                Vk = 0xFF,
                Flags = 2
            }
        }
    }

    ];
    private readonly nint _window;
    private readonly NativeMethods.KeyboardHookProc _callback;
    private readonly Func<bool> _postShortcut;
    private readonly WindowsVGesture _gesture = new();
    private nint _hook;
    private nuint _generation;
    private bool _enabled;

    public WindowsVHotkey(nint window)
    {
        _window = window;
        _callback = OnKeyboard;
        _postShortcut = PostShortcut;
    }

    public bool SetEnabled(bool value)
    {
        if (_enabled == value)
        {
            return true;
        }

        if (!value)
        {
            _enabled = false;
            _generation++;
            RemoveHook();
            return true;
        }

        RemoveHook();
        if (_hook != 0)
        {
            return false;
        }

        _gesture.Reset(vAlreadyDown: IsDown(V));
        _hook = NativeMethods.SetWindowsHookEx(LowLevelKeyboardHook, _callback, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == 0)
        {
            return false;
        }

        _generation++;
        _enabled = true;
        return true;
    }

    public bool IsCurrent(nuint token) => _enabled && token == _generation;

    private nint OnKeyboard(int code, nuint message, nint data)
    {
        if (_enabled && code == 0 && message is 0x0100 or 0x0101 or 0x0104 or 0x0105)
        {
            try
            {
                var key = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(data);
                if (key.VirtualKey == V && (key.Flags & Injected) == 0)
                {
                    var down = message is 0x0100 or 0x0104;
                    var windowsOnly = down && (IsDown(LeftWindows) || IsDown(RightWindows)) && !IsDown(Shift) && !IsDown(Control) && !IsDown(Alt);
                    if (_gesture.Handle(down, windowsOnly, _postShortcut))
                    {
                        return 1;
                    }
                }
            }
            catch
            { /* Never interrupt the keyboard chain if the callback fails. */
            }
        }

        return NativeMethods.CallNextHookEx(_hook, code, message, data);
    }

    private bool PostShortcut() => NativeMethods.SendInput(1, _startMenuMask, _inputSize) == 1 && NativeMethods.PostMessage(_window, NativeMethods.WM_KEEPCLIP_WINDOWS_V, _generation, 0);

    private static bool IsDown(int key) => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0;

    private void RemoveHook()
    {
        if (_hook == 0)
        {
            return;
        }

        if (NativeMethods.UnhookWindowsHookEx(_hook))
        {
            _hook = 0;
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error == InvalidHookHandle)
        {
            _hook = 0;
        }

    }

    public void Dispose()
    {
        SetEnabled(false);
        RemoveHook();
        GC.KeepAlive(_callback);
    }
}
