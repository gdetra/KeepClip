namespace KeepClip.Application;
// Notification thread and clipboard STA worker share only this immutable snapshot.
public sealed class ClipboardSourceTracker
{
    private readonly object _gate = new();
    private ClipboardSourceContext? _latest;

    public void Record(ClipboardSourceContext context)
    {
        lock (_gate)
        {
            _latest = context;
        }
    }

    public ClipboardSourceContext? ForSequence(uint sequence)
    {
        lock (_gate)
        {
            return _latest?.Sequence == sequence ? _latest : null;
        }
    }
}
