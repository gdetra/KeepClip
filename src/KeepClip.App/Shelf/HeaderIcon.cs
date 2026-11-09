using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KeepClip.App.Shelf;

internal static class HeaderIcon
{
    public static byte[]? TrimTransparentMargins(byte[] icon)
    {
        using var input = new MemoryStream(icon, writable: false);
        var frame = BitmapDecoder.Create(input, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None).Frames[0];
        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        // App logos are small. Avoid expanding unusually large legacy images just to trim them.
        if (width > 1024 || height > 1024)
        {
            return icon;
        }

        var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[y * stride + x * 4 + 3] == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < 0)
        {
            return null;
        }

        if (left == 0 && top == 0 && right == width - 1 && bottom == height - 1)
        {
            return icon;
        }

        var bounds = new Int32Rect(left, top, right - left + 1, bottom - top + 1);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(new CroppedBitmap(bitmap, bounds)));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}
