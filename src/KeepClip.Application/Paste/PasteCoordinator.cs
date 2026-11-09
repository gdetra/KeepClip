using KeepClip.Domain.History;

namespace KeepClip.Application.Paste;

public sealed class PasteCoordinator(IClipboardAdapter clipboard, IWindowTarget target, IPasteInput input, Action hide)
{
    public async Task<PasteResult> PasteAsync(
        ClipContent content,
        nint destination,
        CancellationToken ct = default,
        Func<bool>? canContinue = null)
    {
        bool Cancelled() => canContinue is not null && !canContinue();
        PasteResult CancelledResult() => new(PasteOutcome.CopiedOnly, Failure: PasteFailure.Cancelled);
        try
        {
            await clipboard.WriteIfCurrentAsync(content, Guid.NewGuid(), () => !Cancelled(), ct);
        }
        catch (ClipboardRequestCancelledException)
        {
            return new(PasteOutcome.Cancelled, Failure: PasteFailure.Cancelled);
        }

        ct.ThrowIfCancellationRequested();
        if (Cancelled())
        {
            return CancelledResult();
        }

        hide();
        PasteResult Fallback(
            PasteFailure failure) => new(PasteOutcome.CopiedOnly, "Elemento copiato. Torna all'app desiderata e usa Ctrl+V.", failure);
        if (!target.IsValid(destination))
        {
            return Fallback(PasteFailure.MissingDestination);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            if (!await target.TryActivateAsync(destination, timeout.Token))
            {
                return Fallback(PasteFailure.ActivationRejected);
            }

            if (Cancelled())
            {
                return CancelledResult();
            }

            if (!await input.WaitForModifiersReleasedAsync(TimeSpan.FromSeconds(1), timeout.Token))
            {
                return Fallback(PasteFailure.ModifiersHeld);
            }

            if (Cancelled())
            {
                return CancelledResult();
            }

            if (!target.IsValid(destination) || !target.IsForeground(destination))
            {
                return Fallback(PasteFailure.FocusChanged);
            }

            return input.SendPaste() ? new(PasteOutcome.InputSent) : Fallback(PasteFailure.InputRejected);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fallback(PasteFailure.TimedOut);
        }
    }
}
