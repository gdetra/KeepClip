using System.Runtime.InteropServices;
using KeepClip.Application.Paste;

namespace KeepClip.App.Interop;

internal sealed class PasteInput : IPasteInput
{
    public async Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout, CancellationToken ct)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (new[]
            {
                0x01,
                0x02,
                0x04,
                0x10,
                0x11,
                0x12,
                0x5B,
                0x5C
            }.All(key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) == 0))
            {
                return true;
            }

            await Task.Delay(15, ct);
        }

        return false;
    }

    public bool SendPaste()
    {
        static NativeMethods.Input Key(ushort vk, bool up = false) => new()
        {
            Type = 1,
            Data = new()
            {
                Keyboard = new()
                {
                    Vk = vk,
                    Flags = up ? 2u : 0u
                }
            }
        };
        NativeMethods.Input[] keys = [Key(0x11), Key(0x56), Key(0x56, true), Key(0x11, true)];
        var sent = NativeMethods.SendInput(4, keys, Marshal.SizeOf<NativeMethods.Input>());
        if (sent is > 0 and < 4)
        {
            NativeMethods.SendInput(2, [Key(0x56, true), Key(0x11, true)], Marshal.SizeOf<NativeMethods.Input>());
        }

        return sent == 4;
    }
}
