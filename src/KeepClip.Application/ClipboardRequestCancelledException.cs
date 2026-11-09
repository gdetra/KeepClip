namespace KeepClip.Application;

public sealed class ClipboardRequestCancelledException : OperationCanceledException
{
    public ClipboardRequestCancelledException() : base("La richiesta di copia non è più attuale.")
    {
    }
}
