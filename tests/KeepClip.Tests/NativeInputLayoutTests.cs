using System.Runtime.InteropServices;
using KeepClip.App.Interop;

namespace KeepClip.Tests;

public class NativeInputLayoutTests
{
    [Fact]
    public void MouseHookDataMatchesWindowsAbi()
    {
        Assert.Equal(IntPtr.Size == 8 ? 32 : 24, Marshal.SizeOf<NativeMethods.MouseHookData>());
        Assert.Equal((nint)(IntPtr.Size == 8 ? 24 : 20), Marshal.OffsetOf<NativeMethods.MouseHookData>(nameof(NativeMethods.MouseHookData.Extra)));
    }

    [Fact]
    public void InputMatchesWindowsAbi()
    {
        Assert.Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf<NativeMethods.Input>());
        Assert.Equal((nint)(IntPtr.Size == 8 ? 8 : 4), Marshal.OffsetOf<NativeMethods.Input>(nameof(NativeMethods.Input.Data)));
        Assert.Equal((nint)(IntPtr.Size == 8 ? 16 : 12), Marshal.OffsetOf<NativeMethods.KeyboardInput>(nameof(NativeMethods.KeyboardInput.Extra)));
    }
}
