using KeepClip.App.Appearance;
using KeepClip.App.Interop;
using KeepClip.Application.Appearance;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace KeepClip.App.About;

internal sealed partial class AboutWindow : Window
{
    private readonly WindowTheme _theme;

    public AboutWindow(AboutPage page)
    {
        Title = "Informazioni su KeepClip";
        Content = page;
        _theme = new WindowTheme(this, page);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "KeepClip.ico"));
        var scale = NativeMethods.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96d;
        AppWindow.Resize(new SizeInt32((int)(520 * scale), (int)(480 * scale)));
        ((OverlappedPresenter)AppWindow.Presenter).IsMaximizable = false;
    }

    public void ApplyAppearance(AppearanceSettings? settings) => _theme.Apply(settings);
}
