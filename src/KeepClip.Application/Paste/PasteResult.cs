namespace KeepClip.Application.Paste;

public sealed record PasteResult(
    PasteOutcome Outcome,
    string? Message = null,
    PasteFailure Failure = PasteFailure.None);
