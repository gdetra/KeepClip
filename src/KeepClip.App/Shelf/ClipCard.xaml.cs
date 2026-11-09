using KeepClip.Domain.History;
using KeepClip.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace KeepClip.App.Shelf;

public sealed partial class ClipCard : UserControl
{
    private const string FavoriteStar = "\uE734";
    private const string FavoriteStarFilled = "\uE735";

    public event EventHandler<ClipCardEventArgs>? PasteRequested;

    public event EventHandler<ClipCardEventArgs>? PinRequested;

    public event EventHandler<ClipCardEventArgs>? DeleteRequested;

    public ClipCard()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Refresh();
    }

    private void Card_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Buttons belong to this card but must never invoke the card's paste action.
        for (var node = e.OriginalSource as DependencyObject; node is not null && node != this; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ButtonBase)
            {
                return;
            }
        }

        if (DataContext is not ClipEntry entry)
        {
            return;
        }

        e.Handled = true;
        PasteRequested?.Invoke(this, new(entry));
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipEntry entry)
        {
            PinRequested?.Invoke(this, new(entry));
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipEntry entry)
        {
            DeleteRequested?.Invoke(this, new(entry));
        }
    }

    private static string FormatFiles(
        IReadOnlyList<ClipFile> files) => string.Join(Environment.NewLine, files.Take(4).Select(file => (file.IsDirectory ? "📁 " : "📄 ") + Path.GetFileName(file.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))) + (files.Count > 4 ? $"{Environment.NewLine}+ altri {files.Count - 4}" : "");

    private async void Refresh()
    {
        PreviewImage.Source = null;
        PreviewText.Text = "";
        SourceIcon.Source = null;
        SourceFallback.Visibility = Visibility.Visible;
        KindLabel.Text = "";
        TimeLabel.Text = "";
        SourceName.Text = "";
        PinIcon.Glyph = FavoriteStar;
        if (DataContext is not ClipEntry entry)
        {
            return;
        }

        KindLabel.Text = entry.Kind switch
        {
            ClipKind.Text => "Testo",
            ClipKind.Image => "Immagine",
            ClipKind.Files => (entry.Files?.Count ?? 0) + " file",
            _ => ""
        };
        PinIcon.Glyph = entry.IsPinned ? FavoriteStarFilled : FavoriteStar;
        var pinAction = entry.IsPinned ? "Rimuovi dai preferiti" : "Aggiungi ai preferiti";
        ToolTipService.SetToolTip(PinButton, pinAction);
        AutomationProperties.SetName(PinButton, pinAction);
        TimeLabel.Text = entry.CopiedAt.ToLocalTime().ToString("dd MMM, HH:mm");
        PreviewText.Text = entry.Text is { Length: > 1200 } text ? text[..1200] : entry.Text ?? "";
        if (entry.Kind == ClipKind.Files && entry.Files is { } files)
        {
            PreviewText.Text = FormatFiles(files);
        }

        SourceName.Text = entry.Source?.Name ?? "Origine sconosciuta";
        ToolTipService.SetToolTip(SourceName, SourceName.Text);
        if (entry.Source?.IconPng is { Length: > 0 } bytes)
        {
            try
            {
                var cropped = await Task.Run(() => HeaderIcon.TrimTransparentMargins(bytes));
                if (!ReferenceEquals(DataContext, entry))
                {
                    return;
                }

                if (cropped is not null)
                {
                    using var stream = new InMemoryRandomAccessStream();
                    using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                    {
                        writer.WriteBytes(cropped);
                        await writer.StoreAsync();
                        writer.DetachStream();
                    }

                    stream.Seek(0);
                    var icon = new BitmapImage
                    {
                        DecodePixelWidth = 44
                    };
                    await icon.SetSourceAsync(stream);
                    if (!ReferenceEquals(DataContext, entry))
                    {
                        return;
                    }

                    SourceIcon.Source = icon;
                    SourceFallback.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
            }
        }

        if (!ReferenceEquals(DataContext, entry))
        {
            return;
        }

        if (entry.Kind == ClipKind.Files && entry.Files is { } references)
        {
            var available = await Task.Run(() => StoredClipContentLoader.FilesAvailable(references));
            if (ReferenceEquals(DataContext, entry) && !available)
            {
                PreviewText.Text = "Originali non disponibili" + Environment.NewLine + FormatFiles(references);
            }
        }

        if (!ReferenceEquals(DataContext, entry))
        {
            return;
        }

        if (entry.ImagePath is not null)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(entry.ImagePath);
                using var stream = await file.OpenReadAsync();
                var image = new BitmapImage
                {
                    DecodePixelWidth = 440
                };
                await image.SetSourceAsync(stream);
                if (ReferenceEquals(DataContext, entry))
                {
                    PreviewImage.Source = image;
                }
            }
            catch
            {
                if (ReferenceEquals(DataContext, entry))
                {
                    PreviewText.Text = "Anteprima non disponibile";
                }
            }
        }
    }
}
