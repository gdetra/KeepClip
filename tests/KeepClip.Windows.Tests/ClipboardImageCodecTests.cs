using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeepClip.App.Clipboard;

namespace KeepClip.Windows.Tests;

public class ClipboardImageCodecTests
{
    [Fact]
    public void DibV5RoundTripKeepsPixelOrderAndAlpha()
    {
        byte[] pixels = [17, 32, 84, 128, 45, 65, 80, 255];
        var image = BitmapSource.Create(1, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        var dib = ClipboardImageCodec.ToDibV5(stream.ToArray());
        var restored = ClipboardImageCodec.Normalize(dib, isDib: true);
        using var restoredStream = new MemoryStream(restored);
        var frame = new PngBitmapDecoder(restoredStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var actual = new byte[8];
        frame.CopyPixels(actual, 4, 0);
        Assert.Equal(pixels, actual);
    }

    [Fact]
    public void ReadsStandardBottomUp24BitDib()
    {
        byte[] dib = [40, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 1, 0, 24, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 0, 0, 0, 0, 0, 255, 0];
        using var stream = new MemoryStream(ClipboardImageCodec.Normalize(dib, isDib: true));
        var frame = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var pixels = new byte[8];
        new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 4, 0);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, pixels);
    }

    [Fact]
    public void RejectsTruncatedDib() => Assert.Throws<InvalidDataException>(() => ClipboardImageCodec.Normalize([40, 0, 0, 0], isDib: true));
}
