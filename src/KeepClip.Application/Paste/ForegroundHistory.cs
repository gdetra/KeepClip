namespace KeepClip.Application.Paste;

public sealed class ForegroundHistory(Func<nint, bool> isDestination)
{
    private nint _previous;

    public void Observe(nint window)
    {
        if (isDestination(window))
        {
            _previous = window;
        }
    }

    public nint Resolve(nint currentWindow)
    {
        Observe(currentWindow);
        return isDestination(_previous) ? _previous : 0;
    }
}
