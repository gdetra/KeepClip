namespace KeepClip.App.Desktop;

internal sealed class SingleInstanceService : IDisposable
{
    private Mutex? _mutex;
    private bool _acquired;

    public bool TryAcquire()
    {
        _mutex = new Mutex(false, "Local\\KeepClip.Personal.1");
        try
        {
            _acquired = _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            _acquired = true;
        }

        return _acquired;
    }

    public void Dispose()
    {
        if (_acquired)
        {
            _mutex?.ReleaseMutex();
        }

        _mutex?.Dispose();
    }
}
