namespace KeepClip.Application.Paste;

public enum PasteFailure
{
    None,
    MissingDestination,
    ActivationRejected,
    ModifiersHeld,
    FocusChanged,
    InputRejected,
    TimedOut,
    Cancelled,
    OriginalFilesUnavailable
}
