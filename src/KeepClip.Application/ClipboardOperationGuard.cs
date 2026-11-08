using System.Runtime.InteropServices;

namespace KeepClip.Application;

// A sequence can advance when a delayed format is rendered; check the operation
// marker before treating that change as another copy.
public sealed class ClipboardOperationGuard(
    string operationToken,
    Func<uint> sequence,
    Func<CancellationToken, Task<string?>> readOperationToken)
{
    private uint? _confirmedSequence;

    public async Task<bool> StillOwnedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var observed = sequence();
        if (observed == _confirmedSequence)
        {
            return true;
        }

        var token = await readOperationToken(ct);
        ct.ThrowIfCancellationRequested();
        if (sequence() != observed)
        {
            throw new COMException("Gli appunti sono cambiati durante la verifica.", ClipboardFlushRetry.ClipboardBusy);
        }

        if (token != operationToken)
        {
            return false;
        }

        _confirmedSequence = observed;
        return true;
    }
}
