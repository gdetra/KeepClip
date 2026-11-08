using System.ComponentModel;
using KeepClip.App.Appearance;
using KeepClip.App.Clipboard;
using KeepClip.Application.Appearance;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace KeepClip.App.Settings;

internal sealed partial class SettingsWindow : Window
{
    private readonly WindowTheme _theme;
    private bool _recoveryDialogOpen;

    public SettingsWindow(
        SettingsViewModel model)
    {
        Model = model;
        InitializeComponent();
        _theme = new(this, Root);
        CreatePaletteChoices();
        PreviewAppearance();
        Model.PropertyChanged += Model_PropertyChanged;
        Root.Loaded += Root_Loaded;
        Closed += (_, _) => Model.PropertyChanged -= Model_PropertyChanged;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "KeepClip.ico"));
        AppWindow.Resize(new SizeInt32(680, 690));
        ((OverlappedPresenter)AppWindow.Presenter).IsMaximizable = false;
    }

    public SettingsViewModel Model { get; }

    private async void Root_Loaded(
        object sender,
        RoutedEventArgs e) => await Model.RefreshStartupCommand.ExecuteAsync(null);

    private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Model.Appearance))
        {
            PreviewAppearance();
        }
    }

    private void CreatePaletteChoices()
    {
        for (var i = 0; i < ThemeCatalog.Palettes.Count; i++)
        {
            var palette = ThemeCatalog.Palettes[i];
            var content = new StackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(WindowTheme.Color(palette.Accent)) });
            content.Children.Add(new TextBlock { Text = palette.Name, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["CaptionTextBlockStyle"], TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            var button = new ToggleButton
            {
                Tag = palette.Id,
                Content = content,
                Command = Model.SelectPaletteCommand,
                CommandParameter = palette.Id,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(8)
            };
            AutomationProperties.SetName(button, $"{palette.Name}, palette del tema");
            AutomationProperties.SetAutomationId(button, $"Palette_{palette.Id}");
            // Reassert the single selection when the selected ToggleButton is clicked again.
            button.Click += (_, _) => PreviewAppearance();
            ToolTipService.SetToolTip(button, palette.Name);
            Grid.SetRow(button, i / 4);
            Grid.SetColumn(button, i % 4);
            PaletteChoices.Children.Add(button);
        }
    }

    private void PreviewAppearance()
    {
        foreach (var button in PaletteChoices.Children.OfType<ToggleButton>())
        {
            button.IsChecked = (string)button.Tag == Model.SelectedPalette;
        }

        _theme.Apply(Model.Appearance);
    }

    private void PreviewSound_Click(object sender, RoutedEventArgs e)
    {
        var volume = (int)Math.Round(Model.SoundVolume);
        if (volume == 0)
        {
            CopyFeedbackSound.Stop();
            Model.Show("Volume a 0%: il suono è silenzioso.");
        }
        else if (!CopyFeedbackSound.Play(volume))
        {
            Model.Show("Impossibile riprodurre il suono di prova.");
        }
    }

    private async void Recover_Click(object sender, RoutedEventArgs e)
    {
        if (_recoveryDialogOpen || Model.IsBusy)
        {
            return;
        }

        _recoveryDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Root.XamlRoot,
                RequestedTheme = Root.RequestedTheme,
                Title = "Ripristinare la cronologia?",
                Content = "Verrà creata una copia di backup completa della cronologia e delle immagini prima di iniziare una cronologia vuota.",
                PrimaryButtonText = "Ripristina",
                CloseButtonText = "Annulla",
                DefaultButton = ContentDialogButton.Close
            };
            _theme.StyleDialog(dialog);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await Model.RecoverCommand.ExecuteAsync(null);
            }
        }
        finally
        {
            _recoveryDialogOpen = false;
        }
    }
}
