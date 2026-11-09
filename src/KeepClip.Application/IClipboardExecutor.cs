namespace KeepClip.Application;

public interface IClipboardExecutor : IDisposable
{
    Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct);
}
