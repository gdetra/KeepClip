using KeepClip.Application;

namespace KeepClip.Tests;

public class StartupCommandTests
{
    [Fact]
    public void QuotesExecutablePathWithSpacesAndAddsBackgroundArgument()
    {
        Assert.Equal("\"C:\\Users\\Someone\\My Apps\\KeepClip.App.exe\" --autostart", StartupCommand.Build(@"C:\Users\Someone\My Apps\KeepClip.App.exe"));
    }

    [Fact]
    public void RegistrationForAnotherExecutableIsNotReportedEnabled()
    {
        Assert.False(StartupCommand.Matches("\"C:\\Other\\KeepClip.App.exe\" --autostart", @"C:\Apps\KeepClip.App.exe"));
        Assert.True(StartupCommand.Matches("\"c:\\apps\\keepclip.app.exe\" --autostart", @"C:\Apps\KeepClip.App.exe"));
    }

    [Fact]
    public void OnlyStartupLaunchesRemainHidden()
    {
        Assert.True(StartupCommand.ShouldStartHidden(["app", "--autostart"], false));
        Assert.True(StartupCommand.ShouldStartHidden(["app"], true));
        Assert.False(StartupCommand.ShouldStartHidden(["app"], false));
    }
}
