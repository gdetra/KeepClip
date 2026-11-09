namespace KeepClip.App.About;

internal sealed record AppVersionInfo(string Version, string Build, string Distribution)
{
    public static AppVersionInfo From(
        Version? packageVersion,
        string? informationalVersion,
        string fileVersion)
    {
        var numericVersion = informationalVersion?.Split('-', '+')[0];
        var version = packageVersion?.ToString()
            ?? (System.Version.TryParse(numericVersion, out var parsed) ? $"{parsed.Major}.{parsed.Minor}.{parsed.Build}.0" : fileVersion);
        return new AppVersionInfo(version, fileVersion, packageVersion is null ? "Versione portabile" : "Pacchetto MSIX");
    }
}
