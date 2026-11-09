using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeepClip.App.Shelf;

namespace KeepClip.Windows.Tests;

public class HeaderIconTests
{
    [Fact]
    public void TrimsOnlyTransparentMarginsAndPreservesAspectRatio()
    {
        var png = EncodeIcon((x, y) => x is >= 2 and < 6 && y is >= 1 and < 4 ? (byte)128 : (byte)0);
        var trimmed = Assert.IsType<byte[]>(HeaderIcon.TrimTransparentMargins(png));
        using var stream = new MemoryStream(trimmed);
        var image = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Assert.Equal(4, image.PixelWidth);
        Assert.Equal(3, image.PixelHeight);
        byte[] pixels = new byte[4 * 3 * 4];
        image.CopyPixels(pixels, 4 * 4, 0);
        Assert.Equal(128, pixels[3]);
    }

    [Fact]
    public void TightIconsAreReusedAndEmptyIconsUseTheFallback()
    {
        var tight = EncodeIcon((_, _) => 255);
        Assert.Same(tight, HeaderIcon.TrimTransparentMargins(tight));
        Assert.Null(HeaderIcon.TrimTransparentMargins(EncodeIcon((_, _) => 0)));
    }

    private static byte[] EncodeIcon(Func<int, int, byte> alpha)
    {
        const int size = 8;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                pixels[(y * size + x) * 4 + 3] = alpha(x, y);
            }
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
