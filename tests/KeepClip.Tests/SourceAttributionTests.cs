using KeepClip.Application;
using KeepClip.Domain.History;

namespace KeepClip.Tests;

public class SourceAttributionTests
{
    private static ClipSourceCandidate App(string process) => new(process, new ClipSource(process, [1, 2, 3]));

    [Fact]
    public void ActualOwnerWinsEvenWhenFocusChanges()
    {
        var owner = App("notepad");
        Assert.Same(owner.Source, SourceAttribution.Select(owner, App("ms-teams")));
    }

    [Theory]
    [InlineData("ApplicationFrameHost")]
    [InlineData("RuntimeBroker")]
    [InlineData("ShellExperienceHost")]
    [InlineData("TextInputHost")]
    [InlineData("DLLHOST")]
    [InlineData("svchost")]
    [InlineData("taskhostw")]
    [InlineData("rundll32")]
    public void WindowsIntermediaryUsesApplicationAtNotification(string process)
    {
        var application = App("ChatGPT");
        Assert.Same(application.Source, SourceAttribution.Select(App(process), application));
    }

    [Fact]
    public void MissingOwnerUsesNotificationApplication()
    {
        var application = App("ms-teams");
        Assert.Same(application.Source, SourceAttribution.Select(null, application));
    }

    [Fact]
    public void InaccessibleOwnerUsesNotificationApplication()
    {
        var application = App("notepad");
        Assert.Same(application.Source, SourceAttribution.Select(new("unknown", null), application));
    }

    [Fact]
    public void IntermediaryWithoutApplicationDoesNotBecomeSource()
    {
        Assert.Null(SourceAttribution.Select(App("ApplicationFrameHost"), null));
        Assert.Null(SourceAttribution.Select(null, App("ShellExperienceHost")));
    }

    [Fact]
    public void ExplorerRemainsValidForFileCopies()
    {
        var explorer = App("explorer");
        Assert.Same(explorer.Source, SourceAttribution.Select(explorer, App("ChatGPT")));
    }

    [Fact]
    public void NotificationContextIsOnlyUsedForMatchingClipboard()
    {
        var tracker = new ClipboardSourceTracker();
        tracker.Record(new(100, 10, 20));
        Assert.Equal(new ClipboardSourceContext(100, 10, 20), tracker.ForSequence(100));
        Assert.Null(tracker.ForSequence(101));
    }

    [Fact]
    public void NewNotificationCannotAttributePreviousContentToNewApplication()
    {
        var tracker = new ClipboardSourceTracker();
        tracker.Record(new(100, 10, 20));
        tracker.Record(new(101, 30, 40));
        Assert.Null(tracker.ForSequence(100));
        Assert.Equal(40u, tracker.ForSequence(101)!.ForegroundProcess);
    }
}
