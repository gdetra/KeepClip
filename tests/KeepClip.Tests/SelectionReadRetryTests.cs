using KeepClip.Application.Selection;

namespace KeepClip.Tests;

public sealed class SelectionReadRetryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task UnsafeFieldStopsRetryEvenWhenLaterReadWouldReturnText(bool? readOnly)
    {
        var attempts = 0;
        var result = await SelectionReadRetry.ReadAsync(
            _ => Task.FromResult<SelectedText?>(++attempts == 1 ? new(null, IsReadOnly: readOnly) : new("Parola da sostituire")),
            () => true,
            (_, _) => Task.CompletedTask);
        Assert.NotNull(result);
        Assert.Equal(readOnly, result.IsReadOnly);
        Assert.Null(result.Text);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task LateProviderSelectionIsRetried()
    {
        var attempts = 0;
        var waits = new List<int>();
        var result = await SelectionReadRetry.ReadAsync(
            _ => Task.FromResult<SelectedText?>(++attempts < 3 ? null : new("selected")),
            () => true,
            (ms, _) =>
            {
                waits.Add(ms);
                return Task.CompletedTask;
            });
        Assert.Equal("selected", result?.Text);
        Assert.Equal(3, attempts);
        Assert.Equal(3, waits.Count);
    }

    [Fact]
    public async Task ClipboardOrFocusChangeAfterReadRejectsSelection()
    {
        var current = true;
        var result = await SelectionReadRetry.ReadAsync(
            _ =>
            {
                current = false;
                return Task.FromResult<SelectedText?>(new("stale"));
            },
            () => current,
            (_, _) => Task.CompletedTask);
        Assert.Null(result);
    }

    [Fact]
    public async Task PasswordStopsRetry()
    {
        var attempts = 0;
        var result = await SelectionReadRetry.ReadAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult<SelectedText?>(new(null, IsPassword: true));
            },
            () => true,
            (_, _) => Task.CompletedTask);
        Assert.True(result?.IsPassword);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task InvalidatedGestureDoesNotRead()
    {
        var current = true;
        var attempts = 0;
        var result = await SelectionReadRetry.ReadAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult<SelectedText?>(new("stale"));
            },
            () => current,
            (_, _) =>
            {
                current = false;
                return Task.CompletedTask;
            });
        Assert.Null(result);
        Assert.Equal(0, attempts);
    }
}
