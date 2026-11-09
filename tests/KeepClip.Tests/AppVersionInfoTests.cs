using KeepClip.App.About;

namespace KeepClip.Tests;

public sealed class AppVersionInfoTests
{
    [Fact]
    public void InstalledPackageVersionTakesPrecedenceOverExecutableVersion()
    {
        var info = AppVersionInfo.From(new Version(1, 0, 0, 0), "0.2.0-ci.4+Sha.private", "0.2.0.55004");
        Assert.Equal("1.0.0.0", info.Version);
        Assert.Equal("0.2.0.55004", info.Build);
        Assert.Equal("Pacchetto MSIX", info.Distribution);
    }

    [Fact]
    public void PortableShowsOnlyNumericVersionWithoutBranchOrCommitMetadata()
    {
        var info = AppVersionInfo.From(null, "1.1.0-ci.12+Branch.main.Sha.abcdef", "1.1.0.55012");
        Assert.Equal("1.1.0.0", info.Version);
        Assert.Equal("1.1.0.55012", info.Build);
        Assert.Equal("Versione portabile", info.Distribution);
    }

    [Fact]
    public void MissingInformationalVersionUsesTheRealFileVersion()
    {
        var info = AppVersionInfo.From(null, null, "1.0.0.0");
        Assert.Equal("1.0.0.0", info.Version);
    }
}
