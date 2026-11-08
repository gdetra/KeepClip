using System.ComponentModel;
using KeepClip.App.Interop;
using KeepClip.Application;

namespace KeepClip.App.Clipboard;

internal sealed class ClipboardMonitor : IDisposable
{
    private readonly MessageWindow _window;
    private readonly HistoryService _history;
    private readonly ClipboardSourceTracker _sources;
    private readonly ClipboardCapturePump _capture;

    public event Action<string>? Problem;

    public event Action? Copied;

    public ClipboardMonitor(
        MessageWindow window,
        IClipboardAdapter adapter,
        HistoryService history,
        ClipboardSourceTracker sources)
    {
        _window = window;
        _history = history;
        _sources = sources;
        if (!NativeMethods.AddClipboardFormatListener(window.Handle))
        {
            throw new Win32Exception();
        }

        _capture = new(history, adapter.ReadAsync);
        _capture.Captured += OnCaptured;
        _capture.Failed += OnFailed;
        window.Message += OnMessage;
    }

    private void OnMessage(uint message, nuint wp, nint lp)
    {
        if (message != NativeMethods.WM_CLIPBOARDUPDATE)
        {
            return;
        }

        var sequence = NativeMethods.GetClipboardSequenceNumber();
        if (_history.IsPaused)
        {
            return;
        }

        var owner = SourceAppResolver.ClipboardProcess();
        var foreground = SourceAppResolver.ForegroundProcess();
        if (sequence == NativeMethods.GetClipboardSequenceNumber())
        {
            _sources.Record(new(sequence, owner, foreground));
        }

        _capture.Notify();
    }

    private void OnCaptured(CaptureResult result)
    {
        if (result.Captured)
        {
            Copied?.Invoke();
        }

        if (result.Message is not null)
        {
            Problem?.Invoke(result.Message);
        }
    }

    private void OnFailed(Exception ex)
    {
        Problem?.Invoke(ex is InvalidDataException ? ex.Message : $"Impossibile acquisire gli appunti (0x{ex.HResult:X8}). Riprova a copiare l'elemento.");
    }

    public void Dispose()
    {
        _window.Message -= OnMessage;
        NativeMethods.RemoveClipboardFormatListener(_window.Handle);
        _capture.Dispose();
    }
}
