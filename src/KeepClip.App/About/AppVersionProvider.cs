using System.Reflection;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace KeepClip.App.About;

internal static class AppVersionProvider
{
    public static AppVersionInfo Read()
    {
        var assembly = typeof(AppVersionProvider).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Non disponibile";
        Version? packageVersion = null;
        try
        {
            var version = Package.Current.Id.Version;
            packageVersion = new Version(version.Major, version.Minor, version.Build, version.Revision);
        }
        catch (InvalidOperationException)
        {
            // Unpackaged processes have no package identity.
        }
        catch (COMException exception) when (exception.HResult == unchecked((int)0x80073D54))
        {
            // APPMODEL_ERROR_NO_PACKAGE on Windows versions exposing the native HRESULT.
        }

        return AppVersionInfo.From(packageVersion, informationalVersion, fileVersion);
    }
}
