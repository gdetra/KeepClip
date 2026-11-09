using KeepClip.Application.Selection;

namespace KeepClip.Tests;

public sealed class SelectionReadSafetyTests
{
    [Fact]
    public void UnsupportedAttributeCannotAuthorizeAutomaticCopy() => Assert.Null(SelectionReadSafety.ProbeReadOnly(() => new object()));

    [Fact]
    public void ProviderFailureCannotAuthorizeAutomaticCopy()
    {
        var failure = new InvalidOperationException("Provider unavailable");
        Assert.Null(SelectionReadSafety.ProbeReadOnly(() => throw failure));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitAttributeKeepsItsMeaning(
        bool readOnly) => Assert.Equal(readOnly, SelectionReadSafety.ProbeReadOnly(() => readOnly));
}
