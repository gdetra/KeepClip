using System.Runtime.InteropServices;

namespace KeepClip.Application;

public static class ClipboardFlushRetry
{
    public const int ClipboardBusy = unchecked((int)0x800401D0);

    public static Task FlushAsync(
        Action flush,
        Func<bool> stillOwned,
        Func<int, CancellationToken, Task> delay,
        CancellationToken ct = default,
        Action<int, int>? onBusy = null) => FlushAsync(flush, _ => Task.FromResult(stillOwned()), delay, ct, onBusy);

    public static async Task FlushAsync(
        Action flush,
        Func<CancellationToken, Task<bool>> stillOwned,
        Func<int, CancellationToken, Task> delay,
        CancellationToken ct = default,
        Action<int, int>? onBusy = null)
    {
        int[] waits = [40, 100, 200];
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!await stillOwned(ct))
                {
                    throw new ClipboardWriteSupersededException();
                }

                flush();
                return;
            }
            catch (COMException ex) when (ex.HResult == ClipboardBusy && attempt < waits.Length)
            {
                onBusy?.Invoke(attempt + 1, waits[attempt]);
                await delay(waits[attempt], ct);
            }
        }
    }
}
