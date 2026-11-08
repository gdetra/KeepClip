using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using KeepClip.App.Clipboard;
using KeepClip.Application;
using KeepClip.Domain.History;

namespace KeepClip.Tests;

// All clipboard and memory calls use a fake. These tests never access OS clipboard data.
public class NativeClipboardTests
{
    private static ClipFile[] Files => [new(@"C:\Files\KeepClip.dll"), new(@"C:\Files\Résumé 📎.pdf"), new(@"C:\Files\Folder", true)];

    private const string Token = "owner:operation";

    [Fact]
    public async Task PublishesNativeUnicodeFileListCopyEffectAndPrivateTokenTogether()
    {
        var api = new FakeApi();
        var clipboard = new NativeClipboard(() => 42, api);
        await clipboard.WriteAsync(Files, Token, 10, () => true, default);
        var payload = api.Published[15];
        Assert.Equal(20u, BinaryPrimitives.ReadUInt32LittleEndian(payload));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(16)));
        Assert.Equal(string.Join('\0', Files.Select(file => file.Path)) + "\0\0", Encoding.Unicode.GetString(payload, 20, payload.Length - 20));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(api.Published[api.Formats["Preferred DropEffect"]]));
        Assert.Equal(42, api.WriteOwner);
        Assert.Equal(0, api.LiveAllocations);
        Assert.True(clipboard.Read("owner:")?.IsOwnWrite);
    }

    [Fact]
    public async Task BusyClipboardRetriesWithoutLosingFormatsOrAllocations()
    {
        var api = new FakeApi
        {
            BusyAttempts = 2
        };
        var clipboard = new NativeClipboard(() => 42, api);
        await clipboard.WriteAsync(Files, Token, 10, () => true, default);
        Assert.Equal(3, api.OpenAttempts);
        Assert.Equal(1, api.Empties);
        Assert.Equal(3, api.Published.Count);
        Assert.Equal(0, api.LiveAllocations);
    }

    [Fact]
    public async Task NewCopyDuringOpenCannotBeOverwritten()
    {
        var api = new FakeApi
        {
            OnOpen = current => current.Sequence++
        };
        await Assert.ThrowsAsync<ClipboardWriteSupersededException>(() => new NativeClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, default));
        Assert.Equal(0, api.Empties);
        Assert.Empty(api.Published);
        Assert.Equal(0, api.LiveAllocations);
        Assert.False(api.IsOpen);
    }

    [Fact]
    public async Task CancelledSessionBeforeCommitPreservesClipboard()
    {
        var api = new FakeApi();
        await Assert.ThrowsAsync<ClipboardRequestCancelledException>(() => new NativeClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => false, default));
        Assert.Equal(0, api.Empties);
        Assert.Empty(api.Published);
        Assert.Equal(0, api.LiveAllocations);
    }

    [Fact]
    public async Task CancellationDuringOpenReleasesMemoryAndClosesClipboard()
    {
        using var cancellation = new CancellationTokenSource();
        var api = new FakeApi
        {
            OnOpen = _ => cancellation.Cancel()
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new NativeClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, cancellation.Token));
        Assert.Equal(0, api.Empties);
        Assert.Empty(api.Published);
        Assert.Equal(0, api.LiveAllocations);
        Assert.False(api.IsOpen);
    }

    [Fact]
    public async Task PersistentBusyClipboardDoesNotEraseExistingData()
    {
        var api = new FakeApi
        {
            BusyAttempts = 20
        };
        await Assert.ThrowsAsync<COMException>(() => new NativeClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, default));
        Assert.Equal(4, api.OpenAttempts);
        Assert.Equal(0, api.Empties);
        Assert.Equal(0, api.LiveAllocations);
    }

    [Fact]
    public async Task AllocationFailureLeavesClipboardUntouched()
    {
        var api = new FakeApi
        {
            FailAllocation = 2
        };
        await Assert.ThrowsAsync<OutOfMemoryException>(() => new NativeClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, default));
        Assert.Equal(0, api.OpenAttempts);
        Assert.Equal(0, api.LiveAllocations);
    }

    [Fact]
    public async Task FailedPublicationDoesNotExposePartialFileSelection()
    {
        var api = new FakeApi
        {
            FailFormat = 15
        };
        await Assert.ThrowsAsync<Win32Exception>(() => new NativeClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, default));
        Assert.Empty(api.Published);
        Assert.Equal(0, api.LiveAllocations);
        Assert.False(api.IsOpen);
    }

    [Fact]
    public async Task MissingOwnerAndInvalidSelectionDoNotTouchClipboard()
    {
        var api = new FakeApi();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new NativeClipboard(() => 0, api).WriteAsync(Files, Token, 10, () => true, default));
        await Assert.ThrowsAsync<InvalidDataException>(() => new NativeClipboard(() => 42, api).WriteAsync([], Token, 10, () => true, default));
        Assert.Equal(0, api.OpenAttempts);
        Assert.Equal(0, api.Empties);
        Assert.Equal(0, api.LiveAllocations);
    }

    [Fact]
    public void MalformedOrOversizedPrivateTokenIsIgnored()
    {
        var api = new FakeApi();
        var clipboard = new NativeClipboard(() => 42, api);
        var format = api.Formats[NativeClipboard.OwnerFormat];
        api.Published[13] = Encoding.Unicode.GetBytes("text\0");
        api.Published[format] = [1];
        Assert.False(clipboard.Read("owner:")!.IsOwnWrite);
        api.Published[format] = new byte[4096];
        Assert.False(clipboard.Read("owner:")!.IsOwnWrite);
        api.Published[format] = Encoding.Unicode.GetBytes("owner:unterminated");
        Assert.False(clipboard.Read("owner:")!.IsOwnWrite);
        Assert.False(api.IsOpen);
    }

    [Fact]
    public void NativeAllocationPaddingDoesNotHideOwnWriteMarker()
    {
        var api = new FakeApi();
        var clipboard = new NativeClipboard(() => 42, api);
        api.Published[api.Formats[NativeClipboard.OwnerFormat]] = Encoding.Unicode.GetBytes(Token + "\0\0\0");
        Assert.True(clipboard.Read("owner:")?.IsOwnWrite);
    }

    [Fact]
    public async Task PublishesAndReadsNativeUnicodeTextAndItsMarker()
    {
        var api = new FakeApi();
        var clipboard = new NativeClipboard(() => 42, api);
        const string text = "Résumé 📎\r\nSeconda riga";
        await clipboard.WriteTextAsync(text, Token, 10, () => true, default);
        Assert.Equal(Encoding.Unicode.GetBytes(text + "\0"), api.Published[13]);
        var own = Assert.IsType<ClipboardSnapshot>(clipboard.Read("owner:"));
        Assert.True(own.IsOwnWrite);
        Assert.Equal(api.Sequence, own.Sequence);
        var foreign = Assert.IsType<ClipboardSnapshot>(clipboard.Read("other:"));
        Assert.Equal(text, foreign.Content.Text);
        Assert.False(foreign.IsOwnWrite);
        Assert.False(api.IsOpen);
        Assert.Equal(0, api.LiveAllocations);
    }

    [Fact]
    public void RichFormatsTakePriorityOverPlainText()
    {
        var api = new FakeApi();
        var clipboard = new NativeClipboard(() => 42, api);
        api.Published[13] = Encoding.Unicode.GetBytes("plain fallback\0");
        api.Published[15] = [1];
        Assert.Equal(ClipKind.Files, clipboard.Read("owner:")!.Content.Kind);
        api.Published.Remove(15);
        api.Published[17] = [1];
        Assert.Equal(ClipKind.Image, clipboard.Read("owner:", normalizeImage: (bytes, _) => bytes)!.Content.Kind);
        Assert.False(api.IsOpen);
    }

    [Fact]
    public void FileCapturePrefersFileDropAndDetachesPathsBeforeClosingClipboard()
    {
        var api = new FakeApi
        {
            Paths = [@"C:\Files\Résumé 📎.pdf", @"C:\Files\folder"]
        };
        var clipboard = new NativeClipboard(() => 42, api);
        api.Published[15] = [1];
        api.Published[13] = Encoding.Unicode.GetBytes("plain fallback\0");
        var snapshot = Assert.IsType<ClipboardSnapshot>(clipboard.Read("owner:", isDirectory: path => path.EndsWith("folder")));
        Assert.False(api.IsOpen);
        api.Paths = [];
        var files = snapshot.PrepareContent!().Files;
        Assert.Equal(new[] { new ClipFile(@"C:\Files\Résumé 📎.pdf"), new ClipFile(@"C:\Files\folder", true) }, files);
    }

    [Fact]
    public async Task PublishesPngAndDibV5WithOneOwnershipMarker()
    {
        var api = new FakeApi();
        var clipboard = new NativeClipboard(() => 42, api);
        await clipboard.WriteImageAsync([1, 2], [3, 4], Token, 10, () => true, default);
        Assert.Equal(new byte[] { 1, 2 }, api.Published[api.Formats["PNG"]]);
        Assert.Equal(new byte[] { 3, 4 }, api.Published[17]);
        Assert.True(clipboard.Read("owner:")?.IsOwnWrite);
        Assert.Equal(1, api.Empties);
        Assert.Equal(0, api.LiveAllocations);
    }

    private sealed class FakeApi : INativeClipboardApi
    {
        public Dictionary<string, uint> Formats = new();
        public Dictionary<uint, byte[]> Published = new();
        private readonly Dictionary<nint, byte[]> _allocations = new();
        private int _allocationCount;

        public uint Sequence { get; set; } = 10;

        public int BusyAttempts, OpenAttempts, Empties, FailAllocation;
        public uint FailFormat;
        public string[] Paths = [];

        public IReadOnlyList<string> ReadFiles()
        {
            Assert.True(IsOpen);
            return Paths.ToArray();
        }

        public bool IsOpen;
        public nint WriteOwner;
        public Action<FakeApi>? OnOpen;

        public int LiveAllocations => _allocations.Count;

        public uint RegisterFormat(string name)
        {
            if (!Formats.TryGetValue(name, out var value))
            {
                Formats[name] = value = (uint)(49152 + Formats.Count);
            }

            return value;
        }

        public bool Contains(uint format) => Published.ContainsKey(format);

        public bool Open(nint owner)
        {
            OpenAttempts++;
            if (BusyAttempts-- > 0)
            {
                return false;
            }

            IsOpen = true;
            WriteOwner = owner;
            OnOpen?.Invoke(this);
            return true;
        }

        public void Close() => IsOpen = false;

        public bool Empty()
        {
            Assert.True(IsOpen);
            Empties++;
            Published.Clear();
            Sequence++;
            return true;
        }

        public nint Allocate(byte[] bytes)
        {
            if (++_allocationCount == FailAllocation)
            {
                throw new OutOfMemoryException();
            }

            var handle = (nint)_allocationCount;
            _allocations.Add(handle, bytes);
            return handle;
        }

        public void Free(nint handle)
        {
            Assert.True(_allocations.Remove(handle));
        }

        public bool Publish(uint format, nint handle)
        {
            Assert.True(IsOpen);
            if (format == FailFormat)
            {
                return false;
            }

            Published[format] = _allocations[handle];
            _allocations.Remove(handle);
            return true;
        }

        public byte[]? Read(uint format, int maxBytes)
        {
            Assert.True(IsOpen);
            return Published.TryGetValue(format, out var bytes) && bytes.Length <= maxBytes ? bytes : null;
        }
    }
}
