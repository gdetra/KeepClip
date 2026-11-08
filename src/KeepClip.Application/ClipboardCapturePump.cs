namespace KeepClip.Application;

// Notify and its continuations run on the monitor's dispatcher.
public sealed class ClipboardCapturePump(
    HistoryService history,
    Func<CancellationToken, Task<ClipboardSnapshot?>> read) : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private bool _reading, _pending;

    public event Action<CaptureResult>? Captured;

    public event Action<Exception>? Failed;

    public void Notify()
    {
        if (history.IsPaused || _lifetime.IsCancellationRequested)
        {
            return;
        }

        _pending = true;
        if (!_reading)
        {
            _ = ReadPendingAsync(_lifetime.Token);
        }
    }

    private async Task ReadPendingAsync(CancellationToken ct)
    {
        _reading = true;
        try
        {
            while (_pending && !ct.IsCancellationRequested)
            {
                _pending = false;
                var generation = history.CaptureGeneration;
                if (history.IsPaused)
                {
                    continue;
                }

                try
                {
                    var snapshot = await read(ct);
                    // Submit while the snapshot is fresh. HistoryService serializes saves
                    // with selection copies and other history changes.
                    if (snapshot is not null)
                    {
                        _ = SaveAsync(snapshot with { CaptureGeneration = generation }, ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Failed?.Invoke(ex);
                }
            }
        }
        finally
        {
            _reading = false;
        }
    }

    private async Task SaveAsync(ClipboardSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            var result = await history.CaptureAsync(snapshot, ct);
            if (!ct.IsCancellationRequested)
            {
                Captured?.Invoke(result);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }

        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
