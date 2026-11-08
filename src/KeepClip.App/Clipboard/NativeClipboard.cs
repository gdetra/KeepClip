using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using KeepClip.Application;
using KeepClip.Application.History;
using KeepClip.Domain.History;

namespace KeepClip.App.Clipboard;

internal sealed class NativeClipboard
{
    internal const string OwnerFormat = "KeepClip.Native.Owner.v1";
    private const uint UnicodeTextFormat = 13; // CF_UNICODETEXT
    private const uint FileDropFormat = 15; // CF_HDROP
    private const uint DibFormat = 8; // CF_DIB
    private const uint DibV5Format = 17; // CF_DIBV5
    private const int MaxDibBytes = 162 * 1024 * 1024;
    private readonly Func<nint> _ownerWindow;
    private readonly INativeClipboardApi _api;
    private readonly uint _ownerFormat, _effectFormat, _pngFormat;

    public NativeClipboard(Func<nint> ownerWindow, INativeClipboardApi? api = null)
    {
        _ownerWindow = ownerWindow;
        _api = api ?? new WindowsClipboardApi();
        _ownerFormat = _api.RegisterFormat(OwnerFormat);
        _effectFormat = _api.RegisterFormat("Preferred DropEffect");
        _pngFormat = _api.RegisterFormat("PNG");
        if (_ownerFormat == 0 || _effectFormat == 0 || _pngFormat == 0)
        {
            throw new Win32Exception();
        }
    }

    public ClipboardSnapshot? Read(
        string ownerPrefix,
        Func<uint, ClipboardSourceContext>? source = null,
        Func<byte[], bool, byte[]>? normalizeImage = null,
        Func<string, bool>? isDirectory = null)
    {
        if (!_api.Open(0))
        {
            throw Busy();
        }

        try
        {
            if (_api.Contains(_ownerFormat) && DecodeUnicode(_api.Read(_ownerFormat, 2048))?.StartsWith(ownerPrefix, StringComparison.Ordinal) == true)
            {
                return new(new(ClipKind.Text), _api.Sequence, IsOwnWrite: true);
            }

            var context = source?.Invoke(_api.Sequence);
            if (_api.Contains(FileDropFormat))
            {
                var paths = _api.ReadFiles();
                return new(
                    new(ClipKind.Files),
                    _api.Sequence,
                    SourceContext: context,
                    PrepareContent: () => PrepareFiles(paths, isDirectory));
            }

            if (normalizeImage is not null)
            {
                foreach (var format in new[]
                {
                    _pngFormat,
                    DibV5Format,
                    DibFormat
                }

                )
                {
                    if (!_api.Contains(format))
                    {
                        continue;
                    }

                    var bytes = _api.Read(format, format == _pngFormat ? (int)HistoryPolicy.MaxImageBytes : MaxDibBytes);
                    if (bytes is not null)
                    {
                        return new(
                            new(ClipKind.Image),
                            _api.Sequence,
                            SourceContext: context,
                            PrepareContent: () => new(ClipKind.Image, Png: normalizeImage(bytes, format != _pngFormat)));
                    }
                }
            }

            if (!_api.Contains(UnicodeTextFormat))
            {
                return null;
            }

            // A valid UTF-8 entry can require up to twice as many UTF-16 bytes.
            var text = DecodeUnicode(_api.Read(UnicodeTextFormat, (int)(2 * HistoryPolicy.MaxTotalBytes + 2)));
            return text is null ? null : new(new(ClipKind.Text, text), _api.Sequence, SourceContext: context);
        }
        finally
        {
            _api.Close();
        }
    }

    private static ClipContent PrepareFiles(IReadOnlyList<string> paths, Func<string, bool>? isDirectory)
    {
        var files = paths.Select(path => new ClipFile(path)).ToArray();
        if (!FileReferences.Valid(files))
        {
            throw new InvalidDataException("Sono supportati fino a 1000 file o cartelle con un percorso disponibile sul dispositivo.");
        }

        return new(ClipKind.Files, Files: files.Select(file => file with { IsDirectory = isDirectory?.Invoke(file.Path) ?? (File.GetAttributes(file.Path) & FileAttributes.Directory) != 0 }).ToArray());
    }

    private static string? DecodeUnicode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 2)
        {
            return null;
        }

        // GlobalSize can include padding beyond the terminator.
        for (var i = 0; i + 1 < bytes.Length; i += 2)
        {
            if (bytes[i] == 0 && bytes[i + 1] == 0)
            {
                return Encoding.Unicode.GetString(bytes, 0, i);
            }
        }

        return null;
    }

    public Task WriteTextAsync(
        string text,
        string token,
        uint expectedSequence,
        Func<bool> isCurrent,
        CancellationToken ct) => PublishAsync(token, [(UnicodeTextFormat, Encoding.Unicode.GetBytes(text + "\0"))], expectedSequence, isCurrent, ct);

    public Task WriteImageAsync(
        byte[] png,
        byte[] dib,
        string token,
        uint expectedSequence,
        Func<bool> isCurrent,
        CancellationToken ct) => PublishAsync(token, [(_pngFormat, png), (DibV5Format, dib)], expectedSequence, isCurrent, ct);

    public async Task WriteAsync(
        IReadOnlyList<ClipFile> files,
        string token,
        uint expectedSequence,
        Func<bool> isCurrent,
        CancellationToken ct)
    {
        if (!FileReferences.Valid(files))
        {
            throw new InvalidDataException("Selezione di file non valida.");
        }

        // DROPFILES has a fixed 20-byte header followed by UTF-16 MULTI_SZ.
        var paths = Encoding.Unicode.GetBytes(string.Join('\0', files.Select(file => Path.GetFullPath(file.Path))) + "\0\0");
        var dropFiles = new byte[checked(20 + paths.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(dropFiles, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(dropFiles.AsSpan(16), 1); // fWide
        paths.CopyTo(dropFiles, 20);
        await PublishAsync(token, [(_effectFormat, [1, 0, 0, 0]), (FileDropFormat, dropFiles)], expectedSequence, isCurrent, ct);
    }

    private async Task PublishAsync(
        string token,
        (uint Format, byte[] Bytes)[] content,
        uint expectedSequence,
        Func<bool> isCurrent,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 1023 || token.Contains('\0'))
        {
            throw new ArgumentException("Identificatore copia non valido.", nameof(token));
        }

        var window = _ownerWindow();
        if (window == 0)
        {
            throw new InvalidOperationException("Finestra proprietaria degli appunti non disponibile.");
        }

        (uint Format, byte[] Bytes)[] payloads = [(_ownerFormat, Encoding.Unicode.GetBytes(token + "\0")), .. content];
        var handles = new nint[payloads.Length];
        try
        {
            // Allocate everything before EmptyClipboard, preserving old data on OOM.
            for (var i = 0; i < payloads.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                handles[i] = _api.Allocate(payloads[i].Bytes);
            }

            for (var attempt = 0; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                if (!isCurrent())
                {
                    throw new ClipboardRequestCancelledException();
                }

                if (_api.Sequence != expectedSequence)
                {
                    throw new ClipboardWriteSupersededException();
                }

                if (!_api.Open(window))
                {
                    if (attempt >= 3)
                    {
                        throw Busy();
                    }

                    await Task.Delay(attempt switch
                    {
                        0 => 40,
                        1 => 100,
                        _ => 200
                    }, ct);
                    continue;
                }

                try
                {
                    // Lock out competing writers, then recheck immediately before commit.
                    ct.ThrowIfCancellationRequested();
                    if (!isCurrent())
                    {
                        throw new ClipboardRequestCancelledException();
                    }

                    if (_api.Sequence != expectedSequence)
                    {
                        throw new ClipboardWriteSupersededException();
                    }

                    if (!_api.Empty())
                    {
                        throw new Win32Exception();
                    }

                    try
                    {
                        // Publish the user content after the ownership metadata.
                        for (var i = 0; i < payloads.Length; i++)
                        {
                            if (!_api.Publish(payloads[i].Format, handles[i]))
                            {
                                throw new Win32Exception();
                            }

                            handles[i] = 0; // Windows now owns this HGLOBAL.
                        }
                    }
                    catch
                    {
                        _api.Empty();
                        throw;
                    }

                    return;
                }
                finally
                {
                    _api.Close();
                }
            }
        }
        finally
        {
            foreach (var handle in handles)
            {
                if (handle != 0)
                {
                    _api.Free(handle);
                }
            }
        }
    }

    private static COMException Busy() => new("Appunti temporaneamente non disponibili.", ClipboardFlushRetry.ClipboardBusy);
}
