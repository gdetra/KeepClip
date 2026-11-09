using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using KeepClip.App.Interop;
using KeepClip.Application;
using KeepClip.Domain.History;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage.Streams;

namespace KeepClip.App.Clipboard;

internal static class SourceAppResolver
{
    [DllImport("user32.dll")]
    private static extern nint GetClipboardOwner();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inherit, uint process);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint length);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? value);

    private delegate bool EnumWindow(nint window, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(nint parent, EnumWindow callback, nint parameter);

    public static uint ClipboardProcess() => WindowProcess(GetClipboardOwner());

    public static uint ForegroundProcess()
    {
        var window = NativeMethods.GetForegroundWindow();
        return new WindowTarget().IsValid(window) ? WindowProcess(window) : 0;
    }

    private static uint WindowProcess(nint window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var process);
        var className = new StringBuilder(256);
        NativeMethods.GetClassName(window, className, className.Capacity);
        // Windows 10 can host a UWP CoreWindow inside ApplicationFrameHost.
        if (className.ToString() == "ApplicationFrameWindow")
        {
            EnumChildWindows(
                window,
                (child, _) =>
                {
                    var childClass = new StringBuilder(256);
                    NativeMethods.GetClassName(child, childClass, childClass.Capacity);
                    if (childClass.ToString() != "Windows.UI.Core.CoreWindow")
                    {
                        return true;
                    }

                    NativeMethods.GetWindowThreadProcessId(child, out var childProcess);
                    if (childProcess == 0 || childProcess == process)
                    {
                        return true;
                    }

                    process = childProcess;
                    return false;
                },
                0);
        }

        return process == (uint)Environment.ProcessId ? 0 : process;
    }

    public static async Task<ClipSource?> ResolveClipboardAsync(uint ownerProcess, uint foregroundProcess)
    {
        var owner = await ResolveCandidateAsync(ownerProcess);
        ClipSourceCandidate? foreground = null;
        if (SourceAttribution.NeedsForeground(owner) && foregroundProcess != ownerProcess)
        {
            foreground = await ResolveCandidateAsync(foregroundProcess);
        }

        return SourceAttribution.Select(owner, foreground);
    }

    public static async Task<ClipSource?> ResolveAsync(
        uint process) => SourceAttribution.Select(await ResolveCandidateAsync(process), null);

    private sealed record Identity(string ProcessName, string DisplayName, string? Path, string? AppId);

    private static Identity? ReadIdentity(uint process)
    {
        string? path = null, appId = null;
        var handle = OpenProcess(0x1000, false, process); // PROCESS_QUERY_LIMITED_INFORMATION
        if (handle != 0)
        {
            try
            {
                uint pathLength = 32768;
                var buffer = new StringBuilder((int)pathLength);
                if (QueryFullProcessImageName(handle, 0, buffer, ref pathLength))
                {
                    path = buffer.ToString();
                }


                uint length = 0;
                if (GetApplicationUserModelId(handle, ref length, null) == 122 && length is > 0 and < 4096)
                {
                    var value = new StringBuilder((int)length);
                    if (GetApplicationUserModelId(handle, ref length, value) == 0)
                    {
                        appId = value.ToString();
                    }
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }


        var name = path is null ? null : System.IO.Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            try
            {
                using var app = Process.GetProcessById(checked((int)process));
                name = app.ProcessName;
            }
            catch
            {
                return null;
            }
        }

        var displayName = name;
        if (path is not null)
        {
            try
            {
                var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    displayName = description.Trim();
                }
            }
            catch
            {
            }
        }

        return new(name, displayName, path, appId);
    }

    private static async Task<ClipSourceCandidate?> ResolveCandidateAsync(uint process)
    {
        if (process == 0 || process == (uint)Environment.ProcessId)
        {
            return null;
        }

        try
        {
            var identity = await Task.Run(() => ReadIdentity(process));
            if (identity is null)
            {
                return null;
            }

            if (SourceAttribution.IsIntermediary(identity.ProcessName))
            {
                return new(identity.ProcessName, null);
            }

            var name = identity.DisplayName;
            byte[]? icon = null;
            if (identity.AppId is not null && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) && ApiInformation.IsMethodPresent("Windows.ApplicationModel.AppInfo", "GetFromAppUserModelId"))
            {
                try
                {
                    var info = AppInfo.GetFromAppUserModelId(identity.AppId);
                    if (!string.IsNullOrWhiteSpace(info.DisplayInfo.DisplayName))
                    {
                        name = info.DisplayInfo.DisplayName.Trim();
                    }

                    // Failure to read a packaged logo must not discard its friendly name.
                    try
                    {
                        using var stream = await info.DisplayInfo.GetLogo(new Size(64, 64)).OpenReadAsync();
                        if (stream.Size is > 0 and <= 262144)
                        {
                            using var reader = new DataReader(stream);
                            await reader.LoadAsync((uint)stream.Size);
                            icon = new byte[(int)stream.Size];
                            reader.ReadBytes(icon);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                catch (Exception)
                {
                }
            }

            icon ??= await Task.Run(() => ReadIcon(identity.Path));
            return new(identity.ProcessName, new ClipSource(name, icon));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static byte[]? ReadIcon(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            using var original = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (original is null)
            {
                return null;
            }

            using var bitmap = original.ToBitmap();
            using var small = new System.Drawing.Bitmap(bitmap, 64, 64);
            using var output = new MemoryStream();
            small.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

}
