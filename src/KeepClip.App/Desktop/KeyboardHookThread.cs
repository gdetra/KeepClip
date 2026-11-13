using System.Windows.Threading;

namespace KeepClip.App.Desktop;

internal sealed class KeyboardHookThread : IDisposable
{
    private readonly Thread _thread;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public KeyboardHookThread()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() =>
        {
            try
            {
                ready.SetResult(Dispatcher.CurrentDispatcher);
                Dispatcher.Run();
            }
            catch (Exception error)
            {
                ready.TrySetException(error);
            }
        })
        {
            IsBackground = true,
            Name = "KeepClip keyboard hook"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _dispatcher = ready.Task.GetAwaiter().GetResult();
    }

    public T Invoke<T>(Func<T> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _dispatcher.Invoke(action);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispatcher.InvokeShutdown();
        _thread.Join();
    }
}
