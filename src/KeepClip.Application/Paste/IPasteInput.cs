namespace KeepClip.Application.Paste;

public interface IPasteInput
{
    Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout, CancellationToken ct);
    bool SendPaste();
}
