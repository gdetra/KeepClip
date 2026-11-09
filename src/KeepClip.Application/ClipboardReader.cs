namespace KeepClip.Application;

public static class ClipboardReader
{
    public static async Task<ClipboardSnapshot?> ReadWithRetryAsync(
        IClipboardAdapter adapter,
        CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await adapter.ReadAsync(ct);
            }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 2)
            {
                await Task.Delay(attempt == 0 ? 50 : 100, ct);
            }
        }
    }
}
