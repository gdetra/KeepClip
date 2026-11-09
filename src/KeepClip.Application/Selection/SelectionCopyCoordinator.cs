using KeepClip.Domain.History;

namespace KeepClip.Application.Selection;

public sealed class SelectionCopyCoordinator(
    IClipboardAdapter clipboard,
    HistoryService history,
    Func<bool> enabled,
    Func<uint> sequence,
    Action copied,
    Action<string>? problem = null)
{
    public const int MaxCharacters = 100_000;

    public async Task<bool> CopyAsync(SelectedText selection, Func<bool> isCurrent, CancellationToken ct = default)
    {
        if (!enabled() || history.IsPaused || selection.IsPassword || selection.IsOwnApp || selection.IsReadOnly is not true || string.IsNullOrWhiteSpace(selection.Text) || selection.Text.Length > MaxCharacters || !isCurrent())
        {
            return false;
        }

        var generation = history.CaptureGeneration;
        var content = new ClipContent(ClipKind.Text, selection.Text);
        try
        {
            await clipboard.WriteIfCurrentAsync(
                content,
                Guid.NewGuid(),
                () => enabled() && !history.IsPaused && history.CaptureGeneration == generation && isCurrent(),
                ct);
        }
        catch (ClipboardRequestCancelledException)
        {
            return false;
        }

        if (!enabled() || history.IsPaused || history.CaptureGeneration != generation || !isCurrent())
        {
            return false;
        }

        CaptureResult result;
        try
        {
            result = await history.CaptureAsync(new(content, sequence(), CaptureGeneration: generation, Source: selection.Source), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            problem?.Invoke("Il testo è stato copiato, ma non è stato possibile salvarlo nella cronologia.");
            return false;
        }

        if (result.Message is not null)
        {
            problem?.Invoke(result.Message);
        }

        if (result.Captured)
        {
            copied();
        }

        return result.Captured;
    }
}
