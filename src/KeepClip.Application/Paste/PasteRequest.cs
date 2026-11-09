namespace KeepClip.Application.Paste;

public sealed record PasteRequest(nint Destination, long Generation);
