using System.Runtime.InteropServices;

namespace KeepClip.App.Clipboard;

internal static class CopyFeedbackSound
{
    private const uint Async = 0x0001, NoDefault = 0x0002, Memory = 0x0004;
    private static readonly object _gate = new();
    private static byte[]? _original;
    private static GCHandle _buffer;
    private static int _cachedVolume = -1;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(nint sound, nint module, uint flags);

    public static bool Play(int volume)
    {
        lock (_gate)
        {
            volume = Math.Clamp(volume, 0, 100);
            if (volume == 0)
            {
                Stop();
                return true;
            }

            try
            {
                if (!_buffer.IsAllocated || _cachedVolume != volume)
                {
                    _original ??= File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Copy.wav"));
                    var wave = CopySoundWave.WithVolume(_original, volume);
                    Stop();
                    // SND_MEMORY | SND_ASYNC requires a stable buffer until playback stops.
                    _buffer = GCHandle.Alloc(wave, GCHandleType.Pinned);
                    _cachedVolume = volume;
                }

                return PlaySound(_buffer.AddrOfPinnedObject(), 0, Memory | Async | NoDefault);
            }
            catch (IOException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    public static void Stop()
    {
        lock (_gate)
        {
            if (!_buffer.IsAllocated)
            {
                return;
            }

            PlaySound(0, 0, 0);
            _buffer.Free();
            _cachedVolume = -1;
        }
    }
}
