using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using KeepClip.App.Interop;
using KeepClip.Application;
using KeepClip.Application.History;

namespace KeepClip.App.Clipboard;

internal sealed class WindowsClipboardApi : INativeClipboardApi
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetClipboardData(uint format, nint memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalFree(nint memory);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint DragQueryFileW(nint drop, uint index, StringBuilder? path, uint capacity);

    public IReadOnlyList<string> ReadFiles()
    {
        const uint fileDropFormat = 15;
        var drop = NativeMethods.GetClipboardData(fileDropFormat);
        if (drop == 0)
        {
            throw new COMException("Elenco file temporaneamente non disponibile.", ClipboardFlushRetry.ClipboardBusy);
        }

        var count = DragQueryFileW(drop, uint.MaxValue, null, 0);
        if (count is 0 or > FileReferences.MaxItems)
        {
            throw new InvalidDataException("Sono supportati fino a 1000 file o cartelle per copia.");
        }

        var paths = new string[count];
        for (uint index = 0; index < count; index++)
        {
            var length = DragQueryFileW(drop, index, null, 0);
            if (length is 0 or > 32767)
            {
                throw new InvalidDataException("Percorso file negli appunti non valido.");
            }

            var path = new StringBuilder((int)length + 1);
            if (DragQueryFileW(drop, index, path, (uint)path.Capacity) != length)
            {
                throw new InvalidDataException("Elenco file negli appunti non valido.");
            }

            paths[index] = path.ToString();
        }

        return paths;
    }

    public uint Sequence => NativeMethods.GetClipboardSequenceNumber();

    public uint RegisterFormat(string name) => NativeMethods.RegisterClipboardFormat(name);

    public bool Contains(uint format) => NativeMethods.IsClipboardFormatAvailable(format);

    public bool Open(nint owner) => NativeMethods.OpenClipboard(owner);

    public void Close() => NativeMethods.CloseClipboard();

    public bool Empty() => EmptyClipboard();

    public bool Publish(uint format, nint handle) => SetClipboardData(format, handle) != 0;

    public void Free(nint handle) => GlobalFree(handle);

    public nint Allocate(byte[] bytes)
    {
        var handle = GlobalAlloc(0x0042, (nuint)bytes.Length); // GMEM_MOVEABLE | GMEM_ZEROINIT
        if (handle == 0)
        {
            throw new OutOfMemoryException();
        }

        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == 0)
        {
            Free(handle);
            throw new Win32Exception();
        }

        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
        }
        catch
        {
            NativeMethods.GlobalUnlock(handle);
            Free(handle);
            throw;
        }

        NativeMethods.GlobalUnlock(handle);
        return handle;
    }

    public byte[]? Read(uint format, int maxBytes)
    {
        var handle = NativeMethods.GetClipboardData(format);
        if (handle == 0)
        {
            return null;
        }

        var size = NativeMethods.GlobalSize(handle);
        if (size == 0 || size > (nuint)maxBytes)
        {
            return null;
        }

        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            var bytes = new byte[(int)size];
            Marshal.Copy(pointer, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            NativeMethods.GlobalUnlock(handle);
        }
    }
}
