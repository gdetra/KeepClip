using Microsoft.UI.Xaml.Controls;

namespace KeepClip.App.About;

internal sealed partial class AboutPage : Page
{
    public AboutPage(AppVersionInfo model)
    {
        Model = model;
        InitializeComponent();
    }

    public AppVersionInfo Model { get; }
}
