namespace KeepClip.Application.Paste;

public interface IWindowTarget
{
    nint Capture();
    bool IsValid(nint handle);
    Task<bool> TryActivateAsync(nint handle, CancellationToken ct);
    bool IsForeground(nint handle);
}
