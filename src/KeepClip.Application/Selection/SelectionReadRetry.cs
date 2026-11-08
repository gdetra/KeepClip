namespace KeepClip.Application.Selection;

// The caller supplies desktop checks; this helper never accesses the desktop.
public static class SelectionReadRetry
{
    public static async Task<SelectedText?> ReadAsync(
        Func<CancellationToken, Task<SelectedText?>> read,
        Func<bool> current,
        Func<int, CancellationToken, Task> delay,
        CancellationToken ct = default)
    {
        foreach (var wait in new[]
        {
            100,
            150,
            250
        }

        )
        {
            ct.ThrowIfCancellationRequested();
            if (!current())
            {
                return null;
            }

            await delay(wait, ct);
            if (!current())
            {
                return null;
            }

            var selection = await read(ct);
            if (!current())
            {
                return null;
            }

            if (selection is not null && (selection.IsPassword || selection.IsOwnApp || selection.IsReadOnly is not true || !string.IsNullOrWhiteSpace(selection.Text)))
            {
                return selection;
            }
        }

        return null;
    }
}
