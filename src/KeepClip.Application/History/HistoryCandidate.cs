using System.Security.Cryptography;
using System.Text;
using KeepClip.Domain.History;

namespace KeepClip.Application.History;

internal sealed class HistoryCandidate
{
    private readonly ClipContent _content;
    private readonly byte[] _identityBytes;

    private HistoryCandidate(ClipContent content, byte[] identityBytes)
    {
        _content = content;
        _identityBytes = identityBytes;
    }

    public byte[]? ImageBytes => _content.Kind == ClipKind.Image ? _identityBytes : null;

    public static (HistoryCandidate? Candidate, string? Rejection) Prepare(ClipContent content)
    {
        switch (content.Kind)
        {
            case ClipKind.Text when !string.IsNullOrEmpty(content.Text):
                return (new(content, Encoding.UTF8.GetBytes(content.Text)), null);
            case ClipKind.Image when content.Png is { Length: > 0 } png:
                return (new(content, png), null);
            case ClipKind.Files:
                return FileReferences.Valid(content.Files) ? (new(content, FileReferences.Identity(content.Files!)), null) : (null, "Selezione di file non valida: sono supportati fino a 1000 file o cartelle con percorso disponibile.");
            default:
                return (null, null);
        }
    }

    public ClipEntry CreateEntry(Guid id, DateTimeOffset copiedAt, ClipSource? source)
    {
        var hash = Convert.ToHexString(SHA256.HashData(_identityBytes));
        return new(
            id,
            _content.Kind,
            hash,
            copiedAt,
            false,
            _content.Kind == ClipKind.Text ? _content.Text : null,
            null,
            _identityBytes.LongLength,
            source,
            _content.Kind == ClipKind.Files ? _content.Files!.ToArray() : null);
    }
}
