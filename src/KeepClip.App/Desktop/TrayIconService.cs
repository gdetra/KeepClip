using System.Runtime.InteropServices;
using KeepClip.App.Interop;

namespace KeepClip.App.Desktop;

internal sealed class TrayIconService : IDisposable
{
    private readonly MessageWindow _window;
    private readonly uint _explorerRestart = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    private NativeMethods.NotifyIconData _data;
    private readonly System.Drawing.Icon _icon;

    public bool Paused { get; set; }

    public event Action? Open, TogglePause, Settings, Exit;

    public TrayIconService(MessageWindow window)
    {
        _window = window;
        var size = Math.Clamp((int)Math.Round(16 * NativeMethods.GetDpiForWindow(window.Handle) / 96d), 16, 256);
        _icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "KeepClip.ico"), size, size);
        _data = new()
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            Window = window.Handle,
            Id = 1,
            Flags = 7,
            Callback = NativeMethods.WM_TRAY,
            Icon = _icon.Handle,
            Tip = "KeepClip — cronologia appunti",
            Info = "",
            InfoTitle = ""
        };
        window.Message += OnMessage;
    }

    public void Show() => NativeMethods.Shell_NotifyIcon(0, ref _data);

    public void Notify(string message)
    {
        _data.Flags = 0x10;
        _data.InfoTitle = "KeepClip";
        _data.Info = message.Length > 255 ? message[..255] : message;
        _data.InfoFlags = 1;
        NativeMethods.Shell_NotifyIcon(1, ref _data);
        _data.Flags = 7;
    }

    private void OnMessage(uint msg, nuint wp, nint lp)
    {
        if (msg == _explorerRestart)
        {
            Show();
            return;
        }

        if (msg != NativeMethods.WM_TRAY)
        {
            return;
        }

        if ((uint)lp == 0x0202)
        {
            Open?.Invoke();
        }
        else if ((uint)lp == 0x0205 || (uint)lp == 0x007B)
        {
            var menu = NativeMethods.CreatePopupMenu();
            try
            {
                NativeMethods.AppendMenu(menu, 0, 1, "Apri cronologia");
                NativeMethods.AppendMenu(menu, 0, 2, Paused ? "Riprendi cattura" : "Pausa cattura");
                NativeMethods.AppendMenu(menu, 0, 3, "Impostazioni");
                NativeMethods.AppendMenu(menu, 0x800, 0, null);
                NativeMethods.AppendMenu(menu, 0, 4, "Esci");
                NativeMethods.GetCursorPos(out var point);
                NativeMethods.SetForegroundWindow(_window.Handle);
                var selected = NativeMethods.TrackPopupMenu(menu, 0x0102, point.X, point.Y, 0, _window.Handle, 0);
                NativeMethods.PostMessage(_window.Handle, 0, 0, 0);
                switch (selected)
                {
                    case 1:
                        Open?.Invoke();
                        break;
                    case 2:
                        TogglePause?.Invoke();
                        break;
                    case 3:
                        Settings?.Invoke();
                        break;
                    case 4:
                        Exit?.Invoke();
                        break;
                }
            }
            finally
            {
                NativeMethods.DestroyMenu(menu);
            }
        }
    }

    public void Dispose()
    {
        _window.Message -= OnMessage;
        NativeMethods.Shell_NotifyIcon(2, ref _data);
        _icon.Dispose();
    }
}
