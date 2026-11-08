using System.Windows.Threading;
using KeepClip.Application;

namespace KeepClip.App.Clipboard;

internal sealed class StaClipboardExecutor : IClipboardExecutor
{
    private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _lifetime = new();
    private bool _disposed;
    private int _active;

    public StaClipboardExecutor()
    {
        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                _ready.SetResult(dispatcher);
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                _ready.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "KeepClip clipboard STA"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _active++;
        }

        try
        {
            var dispatcher = await _ready.Task.WaitAsync(ct).ConfigureAwait(false);
            var pending = dispatcher.InvokeAsync(
                () =>
                {
                    ct.ThrowIfCancellationRequested();
                    return action();
                },
                DispatcherPriority.Normal,
                ct);
            // Wait until the operation actually ends. A Windows/provider read
            // cannot be forcibly cancelled; releasing early would allow overlap.
            return await pending.Task.Unwrap().ConfigureAwait(false);
        }
        finally
        {
            lock (_lifetime)
            {
                _active--;
                if (_disposed && _active == 0)
                {
                    StopDispatcher();
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_lifetime)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_active == 0)
            {
                StopDispatcher();
            }
        }
    }

    private void StopDispatcher() => _ = _ready.Task.ContinueWith(
        task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                task.Result.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        },
        CancellationToken.None,
        TaskContinuationOptions.None,
        TaskScheduler.Default);
}
