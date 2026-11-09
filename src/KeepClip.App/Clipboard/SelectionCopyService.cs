using System.Runtime.InteropServices;
using KeepClip.App.Interop;
using KeepClip.Application;
using KeepClip.Application.Selection;
using Microsoft.UI.Dispatching;

namespace KeepClip.App.Clipboard;

internal sealed class SelectionCopyService(
    DispatcherQueue dispatcher,
    HistoryService history,
    SelectionCopyCoordinator copy,
    Func<bool> enabled) : IDisposable
{
    private NativeMethods.MouseHookProc? _mouseCallback;
    private nint _hook;
    private long _version, _lastDownTime;
    private NativeMethods.Point _down, _previousDown;
    private uint _downSequence;
    private bool _pressed, _doubleClick, _reading;
    private Request? _pending;
    private CancellationTokenSource? _lifetime;

    public event Action<string>? Problem;

    private sealed record Request(
        NativeMethods.Point Point,
        NativeMethods.Point Start,
        nint Window,
        uint Sequence,
        long Generation,
        long Version,
        CancellationToken Token);

    public bool Start()
    {
        if (_hook != 0)
        {
            return true;
        }

        _mouseCallback ??= OnMouse;
        _lifetime = new();
        _hook = NativeMethods.SetWindowsHookEx(14, _mouseCallback, NativeMethods.GetModuleHandle(null), 0);
        if (_hook != 0)
        {
            return true;
        }

        _lifetime.Dispose();
        _lifetime = null;
        return false;
    }

    public void Stop()
    {
        Interlocked.Increment(ref _version);
        _pressed = false;
        _lastDownTime = 0;
        _pending = null;
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
        if (_hook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        GC.KeepAlive(_mouseCallback);
    }

    private nint OnMouse(int code, nuint message, nint data)
    {
        try
        {
            if (code >= 0 && message is 0x0201 or 0x0202 && enabled())
            {
                var mouse = Marshal.PtrToStructure<NativeMethods.MouseHookData>(data);
                if ((mouse.Flags & 1) == 0)
                {
                    if (message == 0x0201)
                    {
                        Interlocked.Increment(ref _version);
                        _pending = null;
                        _down = mouse.Point;
                        _downSequence = NativeMethods.GetClipboardSequenceNumber();
                        _pressed = true;
                        var now = Environment.TickCount64;
                        _doubleClick = _lastDownTime != 0 && now - _lastDownTime <= NativeMethods.GetDoubleClickTime() && Math.Abs(_down.X - _previousDown.X) <= 4 && Math.Abs(_down.Y - _previousDown.Y) <= 4;
                        _lastDownTime = now;
                        _previousDown = _down;
                    }
                    else if (_pressed)
                    {
                        _pressed = false;
                        var drag = Math.Abs(mouse.Point.X - _down.X) >= 3 || Math.Abs(mouse.Point.Y - _down.Y) >= 3;
                        var shift = (NativeMethods.GetAsyncKeyState(0x10) & 0x8000) != 0;
                        var modifiers = new[]
                        {
                            0x11,
                            0x12,
                            0x5B,
                            0x5C
                        }.Any(key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0);
                        var window = NativeMethods.GetForegroundWindow();
                        NativeMethods.GetWindowThreadProcessId(window, out var process);
                        if ((drag || _doubleClick || shift) && !modifiers && process != 0 && process != Environment.ProcessId && _lifetime is not null)
                        {
                            var request = new Request(
                                mouse.Point,
                                _down,
                                window,
                                _downSequence,
                                history.CaptureGeneration,
                                Interlocked.Increment(ref _version),
                                _lifetime.Token);
                            dispatcher.TryEnqueue(() =>
                            {
                                if (!Current(request))
                                {
                                    return;
                                }

                                _pending = request;
                                if (!_reading)
                                {
                                    ReadPending();
                                }
                            });
                        }
                    }
                }
            }
        }
        catch
        { /* Never let an exception cross the native callback boundary. */
        }

        return NativeMethods.CallNextHookEx(_hook, code, message, data);
    }

    private bool Current(
        Request request) => !request.Token.IsCancellationRequested && request.Version == Volatile.Read(ref _version) && enabled() && !history.IsPaused && history.CaptureGeneration == request.Generation && NativeMethods.GetForegroundWindow() == request.Window;

    private async void ReadPending()
    {
        _reading = true;
        try
        {
            while (_pending is { } request)
            {
                _pending = null;
                try
                {
                    NativeMethods.GetWindowThreadProcessId(request.Window, out var process);
                    bool CanRead() => Current(request) && NativeMethods.GetClipboardSequenceNumber() == request.Sequence;
                    var text = await SelectionReadRetry.ReadAsync(
                        async token =>
                        {
                            try
                            {
                                return await Task.Run(() => SelectedTextReader.Read(
                                    request.Point,
                                    request.Start,
                                    process), token);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception)
                            {
                                return null;
                            }
                        },
                        CanRead,
                        (ms, token) => Task.Delay(ms, token),
                        request.Token);
                    if (text is null || text.IsPassword || text.IsOwnApp || text.IsReadOnly is not true || !CanRead())
                    {
                        continue;
                    }

                    var origin = await SourceAppResolver.ResolveAsync(process);
                    if (!CanRead())
                    {
                        continue;
                    }

                    await copy.CopyAsync(text with { Source = origin }, () => Current(request), request.Token);
                }
                catch (ClipboardWriteSupersededException)
                {
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                    Problem?.Invoke("Impossibile completare la copia alla selezione. Riprova con Ctrl+C.");
                }
            }
        }
        finally
        {
            _reading = false;
        }
    }

    public void Dispose() => Stop();
}
