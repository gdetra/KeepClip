using KeepClip.App.Interop;
using KeepClip.Application;
using KeepClip.Application.History;
using KeepClip.Domain.History;
using KeepClip.Storage;

namespace KeepClip.App.Clipboard;

internal sealed class ClipboardAdapter(ClipboardSourceTracker sourceTracker, Func<nint> ownerWindow) : IClipboardAdapter
{
    private readonly NativeClipboard _nativeClipboard = new(ownerWindow);
    private readonly string _owner = Guid.NewGuid().ToString("N");

    public Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_nativeClipboard.Read(_owner + ":", GetSourceContext, ClipboardImageCodec.Normalize));
    }

    private ClipboardSourceContext GetSourceContext(
        uint sequence) => sourceTracker.ForSequence(sequence) ?? new(sequence, SourceAppResolver.ClipboardProcess(), 0);

    public Task WriteAsync(
        ClipContent content,
        Guid operationId,
        CancellationToken ct = default) => WriteIfCurrentAsync(content, operationId, static () => true, ct);

    public async Task WriteIfCurrentAsync(
        ClipContent content,
        Guid operationId,
        Func<bool> isCurrent,
        CancellationToken ct = default)
    {
        var expectedSequence = NativeMethods.GetClipboardSequenceNumber();
        var token = _owner + ":" + operationId.ToString("N");
        ct.ThrowIfCancellationRequested();
        switch (content.Kind)
        {
            case ClipKind.Text:
                await _nativeClipboard.WriteTextAsync(content.Text ?? "", token, expectedSequence, isCurrent, ct);
                break;
            case ClipKind.Files:
                if (!FileReferences.Valid(content.Files))
                {
                    throw new InvalidDataException("Selezione di file non valida.");
                }

                if (!await Task.Run(() => StoredClipContentLoader.FilesAvailable(content.Files!), ct))
                {
                    throw new IOException("Uno o più file originali non sono più disponibili o accessibili.");
                }

                await _nativeClipboard.WriteAsync(content.Files!, token, expectedSequence, isCurrent, ct);
                break;
            case ClipKind.Image:
                if (content.Png is not { Length: > 0 } png)
                {
                    throw new InvalidDataException("Immagine non disponibile.");
                }

                var dib = await Task.Run(() => ClipboardImageCodec.ToDibV5(png), ct);
                await _nativeClipboard.WriteImageAsync(png, dib, token, expectedSequence, isCurrent, ct);
                break;
            default:
                throw new InvalidDataException("Tipo di contenuto non supportato.");
        }
    }
}
