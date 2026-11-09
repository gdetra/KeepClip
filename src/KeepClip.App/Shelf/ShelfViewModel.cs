using CommunityToolkit.Mvvm.ComponentModel;
using KeepClip.Domain.History;

namespace KeepClip.App.Shelf;

public sealed partial class ShelfViewModel : ObservableObject
{
    private IReadOnlyList<ClipEntry> _allEntries = [];

    [ObservableProperty]
    public partial IReadOnlyList<ClipEntry> Entries { get; private set; } = [];

    [ObservableProperty]
    public partial ClipEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowPinnedOnly { get; set; }

    public void Refresh(IReadOnlyList<ClipEntry> entries)
    {
        _allEntries = entries;
        Filter();
    }

    partial void OnQueryChanged(string value) => Filter();

    partial void OnShowPinnedOnlyChanged(bool value) => Filter();

    private void Filter()
    {
        var selectedId = SelectedEntry?.Id;
        Entries = _allEntries.Where(MatchesFilter).ToArray();
        SelectedEntry = Entries.FirstOrDefault(entry => entry.Id == selectedId) ?? Entries.FirstOrDefault();
    }

    private bool MatchesFilter(ClipEntry entry)
    {
        if (ShowPinnedOnly && !entry.IsPinned)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(Query) || (entry.Text?.Contains(Query, StringComparison.OrdinalIgnoreCase) ?? false) || (entry.Files?.Any(file => Path.GetFileName(file.Path).Contains(Query, StringComparison.OrdinalIgnoreCase)) ?? false);
    }
}
