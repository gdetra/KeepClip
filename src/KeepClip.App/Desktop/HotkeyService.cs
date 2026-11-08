using KeepClip.App.Interop;
using KeepClip.Application;
using KeepClip.Application.Settings;

namespace KeepClip.App.Desktop;

internal sealed class HotkeyService : IHotkeySettingsService, IDisposable
{
    private readonly MessageWindow _window;
    private readonly WindowsVHotkey _windowsV;
    private int _registeredId;

    public Hotkey? Current { get; private set; }

    public event Action? Invoked;

    public HotkeyService(MessageWindow window)
    {
        _window = window;
        _windowsV = new(window.Handle);
        window.Message += OnMessage;
    }

    public bool SetWindowsV(bool enabled) => _windowsV.SetEnabled(enabled);

    public bool TryRegister(Hotkey hotkey)
    {
        if (hotkey == Current)
        {
            return true;
        }

        if (hotkey.Modifiers == 0 || hotkey.Key is < 0x41 or > 0x5A)
        {
            return false;
        }

        var nextId = _registeredId == 1 ? 2 : 1;
        if (!NativeMethods.RegisterHotKey(_window.Handle, nextId, hotkey.Modifiers | 0x4000, hotkey.Key))
        {
            return false;
        }

        if (_registeredId != 0)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, _registeredId);
        }

        _registeredId = nextId;
        Current = hotkey;
        return true;
    }

    private void OnMessage(uint message, nuint wp, nint lp)
    {
        if ((message == NativeMethods.WM_HOTKEY && _registeredId != 0 && (int)wp == _registeredId) || (message == NativeMethods.WM_KEEPCLIP_WINDOWS_V && _windowsV.IsCurrent(wp)))
        {
            Invoked?.Invoke();
        }
    }

    public void Dispose()
    {
        _window.Message -= OnMessage;
        _windowsV.Dispose();
        if (_registeredId != 0)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, _registeredId);
        }
    }
}
