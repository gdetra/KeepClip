using KeepClip.Application.Appearance;
using Microsoft.UI.System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace KeepClip.App.Appearance;

internal sealed class WindowTheme : IDisposable
{
    private readonly Window _window;
    private readonly FrameworkElement _root;
    private readonly UISettings _ui = new();
    private readonly ThemeSettings _themeSettings;
    private AppearanceSettings _settings = new();
    private bool _disposed;

    public WindowTheme(Window window, FrameworkElement root)
    {
        _window = window;
        _root = root;
        // AccessibilitySettings.HighContrastChanged requires UWP UI infrastructure.
        // ThemeSettings is the Windows App SDK equivalent for Win32/WinUI windows.
        _themeSettings = ThemeSettings.CreateForWindowId(window.AppWindow.Id);
        _ui.ColorValuesChanged += ColorsChanged;
        _themeSettings.Changed += ContrastChanged;
        root.Loaded += RootLoaded;
        window.Closed += (_, _) => Dispose();
    }

    public void Apply(AppearanceSettings? next)
    {
        _settings = ThemeCatalog.Normalize(next);
        Refresh();
    }

    public void StyleDialog(ContentDialog dialog)
    {
        // Popups do not inherit the window root's resource scope.
        foreach (var resource in _root.Resources)
        {
            dialog.Resources[resource.Key] = resource.Value;
        }

        dialog.SetBinding(FrameworkElement.RequestedThemeProperty, new Binding { Source = _root, Path = new PropertyPath(nameof(FrameworkElement.RequestedTheme)), Mode = BindingMode.OneWay });
    }

    private void RootLoaded(object sender, RoutedEventArgs args) => Refresh();

    private void ColorsChanged(UISettings sender, object args) => QueueRefresh();

    private void ContrastChanged(ThemeSettings sender, object args) => QueueRefresh();

    private void QueueRefresh() => _root.DispatcherQueue.TryEnqueue(() =>
    {
        if (!_disposed)
        {
            Refresh();
        }
    });

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var systemBackground = _ui.GetColorValue(UIColorType.Background);
        var colors = ThemeCatalog.Resolve(_settings, systemBackground.R + systemBackground.G + systemBackground.B < 384);
        _root.RequestedTheme = colors.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        if (_themeSettings.HighContrast)
        {
            var page = Pack(_ui.UIElementColor(UIElementType.Window));
            var text = Pack(_ui.UIElementColor(UIElementType.WindowText));
            var accent = Pack(_ui.UIElementColor(UIElementType.Highlight));
            var onAccent = Pack(_ui.UIElementColor(UIElementType.HighlightText));
            colors = colors with
            {
                Background = page,
                Surface = page,
                Header = page,
                Text = text,
                SecondaryText = text,
                HeaderText = text,
                Border = text,
                Accent = accent,
                AccentText = text,
                AccentForeground = onAccent,
                AccentHover = accent,
                AccentPressed = accent,
                Selection = page
            };
        }

        Set("KeepClipPageBrush", colors.Background);
        Set("KeepClipSurfaceBrush", colors.Surface);
        Set("KeepClipHeaderBrush", colors.Header);
        Set("KeepClipTextBrush", colors.Text);
        Set("KeepClipSecondaryTextBrush", colors.SecondaryText);
        Set("KeepClipHeaderTextBrush", colors.HeaderText);
        Set("KeepClipBorderBrush", colors.Border);
        Set("KeepClipAccentBrush", colors.Accent);
        Set("KeepClipAccentTextBrush", colors.AccentText);
        Set("KeepClipAccentForegroundBrush", colors.AccentForeground);
        Set("KeepClipAccentHoverBrush", colors.AccentHover);
        Set("KeepClipAccentPressedBrush", colors.AccentPressed);
        Set("KeepClipSelectionBrush", colors.Selection);
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var bar = _window.AppWindow.TitleBar;
            bar.BackgroundColor = bar.InactiveBackgroundColor = bar.ButtonBackgroundColor = bar.ButtonInactiveBackgroundColor = Color(colors.Background);
            bar.ForegroundColor = bar.ButtonForegroundColor = Color(colors.Text);
            bar.InactiveForegroundColor = bar.ButtonInactiveForegroundColor = Color(colors.SecondaryText);
            bar.ButtonHoverBackgroundColor = Color(colors.Surface);
            bar.ButtonHoverForegroundColor = Color(colors.Text);
        }
    }

    private void Set(string key, uint value) => ((SolidColorBrush)_root.Resources[key]).Color = Color(value);

    internal static Color Color(
        uint value) => Windows.UI.Color.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);

    private static uint Pack(Color color) => (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ui.ColorValuesChanged -= ColorsChanged;
        _themeSettings.Changed -= ContrastChanged;
        _root.Loaded -= RootLoaded;
    }
}
