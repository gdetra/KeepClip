using KeepClip.Domain.History;

namespace KeepClip.Application;

// One operation at a time, including awaits and retries. The platform executor
// owns the apartment; clients only exchange managed clipboard snapshots.
public sealed class AsyncClipboardService : IClipboardAdapter, IDisposable
{
    private readonly IClipboardAdapter _backend;
    private readonly IClipboardExecutor _executor;
    private readonly Func<uint> _sequence;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _stopping;
    private int _disposed;

    public AsyncClipboardService(IClipboardAdapter backend, IClipboardExecutor executor, Func<uint> sequence)
    {
        _backend = backend;
        _executor = executor;
        _sequence = sequence;
        _stopping = _lifetime.Token;
    }

    public Task<ClipboardSnapshot?> ReadAsync(
        CancellationToken ct = default) => RunAsync(token => ClipboardReader.ReadWithRetryAsync(_backend, token), ct);

    public Task WriteAsync(
        ClipContent content,
        Guid operationId,
        CancellationToken ct = default) => WriteIfCurrentAsync(content, operationId, static () => true, ct);

    public async Task WriteIfCurrentAsync(
        ClipContent content,
        Guid operationId,
        Func<bool> isCurrent,
        CancellationToken ct = default)
    {
        var expectedSequence = _sequence();
        await RunAsync(async token =>
        {
            // Recheck in the apartment, after both the queue and dispatch waits.
            if (_sequence() != expectedSequence)
            {
                throw new ClipboardWriteSupersededException();
            }

            if (!isCurrent())
            {
                throw new ClipboardRequestCancelledException();
            }

            await _backend.WriteIfCurrentAsync(content, operationId, isCurrent, token);
            return true;
        }, ct).ConfigureAwait(false);
    }

    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopping);
        var token = linked.Token;
        await _operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await _executor.RunAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                return operation(token);
            }, token).ConfigureAwait(false);
        }
        finally
        {
            _operations.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetime.Cancel();
        _executor.Dispose();
        _lifetime.Dispose();
        // Do not dispose the semaphore while cancelled waiters are unwinding.
    }
}
