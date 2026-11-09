namespace KeepClip.App.Desktop;

internal sealed class WindowsVGesture
{
    private bool _vDown, _swallowed;

    public void Reset(bool vAlreadyDown)
    {
        _vDown = vAlreadyDown;
        _swallowed = false;
    }

    public bool Handle(bool down, bool windowsOnly, Func<bool> invoke)
    {
        if (!down)
        {
            var swallowRelease = _swallowed;
            Reset(vAlreadyDown: false);
            return swallowRelease;
        }

        if (_vDown)
        {
            return _swallowed;
        }

        _vDown = true;
        _swallowed = windowsOnly && invoke();
        return _swallowed;
    }
}
