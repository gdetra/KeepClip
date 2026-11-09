namespace KeepClip.Application;

public static class StartupCommand
{
    public static string Build(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (executablePath.Contains('"'))
        {
            throw new ArgumentException("Percorso non valido.", nameof(executablePath));
        }

        return $"\"{executablePath}\" --autostart";
    }

    public static bool Matches(
        string? command,
        string executablePath) => string.Equals(command?.Trim(), Build(executablePath), StringComparison.OrdinalIgnoreCase);

    public static bool ShouldStartHidden(
        IEnumerable<string> arguments,
        bool isStartupActivation) => isStartupActivation || arguments.Contains("--autostart", StringComparer.OrdinalIgnoreCase);
}
