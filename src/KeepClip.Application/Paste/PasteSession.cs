namespace KeepClip.Application.Paste;

public sealed class PasteSession
{
    private long _generation;
    private nint _destination;

    public void Open(nint destination)
    {
        _destination = destination;
        Interlocked.Increment(ref _generation);
    }

    public void Cancel()
    {
        _destination = 0;
        Interlocked.Increment(ref _generation);
    }

    public PasteRequest Snapshot() => new(_destination, _generation);

    public bool IsCurrent(PasteRequest request) => request.Generation == Volatile.Read(ref _generation);
}
