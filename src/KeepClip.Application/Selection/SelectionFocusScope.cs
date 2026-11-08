namespace KeepClip.Application.Selection;

public static class SelectionFocusScope
{
    public static bool SharesTextOwner<T>(
        IEnumerable<T> gesture,
        IEnumerable<T> focus,
        Func<T, bool> isTextOwner,
        Func<T, T, bool> same) => gesture.Any(owner => isTextOwner(owner) && focus.Any(candidate => same(owner, candidate)));
}
