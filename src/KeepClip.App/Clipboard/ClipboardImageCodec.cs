using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeepClip.Domain.History;

namespace KeepClip.App.Clipboard;

internal static class ClipboardImageCodec
{
    private const int FileHeaderSize = 14;
    private const int V5HeaderSize = 124;
    private const uint Bitfields = 3;
    private const uint RedMask = 0x00ff0000, GreenMask = 0x0000ff00, BlueMask = 0x000000ff, AlphaMask = 0xff000000;
    private const BitmapCreateOptions DecodeOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile;

    public static byte[] Normalize(byte[] data, bool isDib)
    {
        try
        {
            var bitmap = isDib ? ReadDib(data) : ReadPng(data);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(AsBgra32(bitmap)));
            using var output = new MemoryStream();
            encoder.Save(output);
            if (output.Length > HistoryPolicy.MaxImageBytes)
            {
                throw new InvalidDataException("Immagine troppo grande: massimo 20 MiB.");
            }

            return output.ToArray();
        }
        catch (Exception ex) when (ex is FileFormatException or NotSupportedException or ArgumentException or COMException)
        {
            throw new InvalidDataException("Formato immagine negli appunti non valido o non supportato.", ex);
        }
    }

    public static byte[] ToDibV5(byte[] png)
    {
        var bitmap = AsBgra32(ReadPng(png));
        var stride = checked(bitmap.PixelWidth * 4);
        var pixelBytes = checked(stride * bitmap.PixelHeight);
        var dib = new byte[V5HeaderSize + pixelBytes];
        Write32(dib, 0, V5HeaderSize);
        Write32(dib, 4, (uint)bitmap.PixelWidth);
        Write32(dib, 8, unchecked((uint)-bitmap.PixelHeight)); // Top-down, same row order as WPF.
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(14), 32);
        Write32(dib, 16, Bitfields);
        Write32(dib, 20, (uint)pixelBytes);
        Write32(dib, 24, PixelsPerMeter(bitmap.DpiX));
        Write32(dib, 28, PixelsPerMeter(bitmap.DpiY));
        Write32(dib, 40, RedMask);
        Write32(dib, 44, GreenMask);
        Write32(dib, 48, BlueMask);
        Write32(dib, 52, AlphaMask);
        Write32(dib, 56, 0x73524742); // LCS_sRGB
        Write32(dib, 108, 4); // LCS_GM_IMAGES
        bitmap.CopyPixels(dib, stride, V5HeaderSize);
        return dib;
    }

    private static BitmapSource ReadPng(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        return LoadPixels(new PngBitmapDecoder(stream, DecodeOptions, BitmapCacheOption.None).Frames[0]);
    }

    private static BitmapSource ReadDib(byte[] data)
    {
        if (data.Length < 12)
        {
            throw InvalidDib();
        }

        var headerSize = Read32(data, 0);
        if (headerSize is not (12 or 40 or 52 or 56 or 108 or V5HeaderSize) || headerSize > data.Length)
        {
            throw InvalidDib();
        }

        var coreHeader = headerSize == 12;
        var width = coreHeader ? Read16(data, 4) : (int)Read32(data, 4);
        var signedHeight = coreHeader ? Read16(data, 6) : (int)Read32(data, 8);
        if (signedHeight == int.MinValue)
        {
            throw InvalidDib();
        }

        var height = Math.Abs(signedHeight);
        CheckDimensions(width, height);
        var depth = Read16(data, coreHeader ? 10 : 14);
        var compression = coreHeader ? 0 : Read32(data, 16);
        var paletteCount = coreHeader ? 0 : Read32(data, 32);
        if (paletteCount == 0 && depth is 1 or 2 or 4 or 8)
        {
            paletteCount = 1u << depth;
        }

        var masks = headerSize == 40 ? compression switch
        {
            Bitfields => 12,
            6 => 16,
            _ => 0
        } : 0;
        var pixelsAt = (long)headerSize + masks + (long)paletteCount * (coreHeader ? 3 : 4);
        if (pixelsAt >= data.Length)
        {
            throw InvalidDib();
        }

        if (compression is 0 or Bitfields or 6)
        {
            if (depth is not (1 or 2 or 4 or 8 or 16 or 24 or 32))
            {
                throw InvalidDib();
            }

            var stride = ((long)width * depth + 31) / 32 * 4;
            if (stride * height > data.Length - pixelsAt)
            {
                throw InvalidDib();
            }
        }

        // WIC's BMP decoder can discard the alpha channel. Preserve it for the
        // common 32-bit BGRA masks used by DIBV5 sources.
        if (headerSize >= 56 && depth == 32 && compression is Bitfields or 6 && Read32(data, 40) == RedMask && Read32(data, 44) == GreenMask && Read32(data, 48) == BlueMask && Read32(data, 52) == AlphaMask)
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var row = 0; row < height; row++)
            {
                var sourceRow = signedHeight < 0 ? row : height - 1 - row;
                Buffer.BlockCopy(data, (int)pixelsAt + sourceRow * stride, pixels, row * stride, stride);
            }

            return BitmapSource.Create(width, height, ToDpi(data, 24), ToDpi(data, 28), PixelFormats.Bgra32, null, pixels, stride);
        }

        var bmp = new byte[FileHeaderSize + data.Length];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        Write32(bmp, 2, (uint)bmp.Length);
        Write32(bmp, 10, FileHeaderSize + (uint)pixelsAt);
        data.CopyTo(bmp, FileHeaderSize);
        using var stream = new MemoryStream(bmp, writable: false);
        return LoadPixels(new BmpBitmapDecoder(stream, DecodeOptions, BitmapCacheOption.None).Frames[0]);
    }

    private static BitmapSource LoadPixels(BitmapSource source)
    {
        CheckDimensions(source.PixelWidth, source.PixelHeight);
        var converted = AsBgra32(source);
        var stride = checked(converted.PixelWidth * 4);
        var pixels = new byte[checked(stride * converted.PixelHeight)];
        converted.CopyPixels(pixels, stride, 0);
        return BitmapSource.Create(
            converted.PixelWidth,
            converted.PixelHeight,
            converted.DpiX,
            converted.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
    }

    private static BitmapSource AsBgra32(
        BitmapSource bitmap) => bitmap.Format == PixelFormats.Bgra32 ? bitmap : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

    private static void CheckDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw InvalidDib();
        }

        if ((long)width * height > 40_000_000)
        {
            throw new InvalidDataException("Immagine troppo grande: massimo 40 megapixel.");
        }
    }

    private static InvalidDataException InvalidDib() => new("Bitmap negli appunti non valido.");

    private static double ToDpi(
        byte[] data,
        int offset) => (int)Read32(data, offset) is > 0 and var ppm ? ppm * 0.0254 : 96;

    private static uint PixelsPerMeter(
        double dpi) => double.IsFinite(dpi) && dpi > 0 ? (uint)Math.Clamp(Math.Round(dpi / 0.0254), 0, int.MaxValue) : 0;

    private static ushort Read16(
        byte[] data,
        int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));

    private static uint Read32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));

    private static void Write32(
        byte[] data,
        int offset,
        uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
}
