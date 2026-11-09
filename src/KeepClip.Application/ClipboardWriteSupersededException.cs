namespace KeepClip.Application;

public sealed class ClipboardWriteSupersededException : OperationCanceledException
{
    public ClipboardWriteSupersededException() : base("Gli appunti sono stati sostituiti da una nuova copia.")
    {
    }
}
