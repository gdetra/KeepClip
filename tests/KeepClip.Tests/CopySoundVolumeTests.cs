using System.Buffers.Binary;
using System.Text.Json;
using KeepClip.App.Clipboard;
using KeepClip.Application;

namespace KeepClip.Tests;

public class CopySoundVolumeTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(25, 500, -500, 10000, -10000)]
    [InlineData(50, 1000, -1000, 20000, -20000)]
    [InlineData(100, 2000, -2000, 32767, -32768)]
    [InlineData(-10, 0, 0, 0, 0)]
    [InlineData(150, 2000, -2000, 32767, -32768)]
    public void VolumeScalesSamplesWithoutChangingOriginalOrWrapping(int volume, short a, short b, short c, short d)
    {
        var wave = Wave();
        var original = (byte[])wave.Clone();
        var scaled = CopySoundWave.WithVolume(wave, volume);
        Assert.Equal(new short[] { a, b, c, d }, Enumerable.Range(0, 4).Select(i => BinaryPrimitives.ReadInt16LittleEndian(scaled.AsSpan(44 + 2 * i))));
        Assert.Equal(original, wave);
        Assert.Equal(original[..44], scaled[..44]);
    }

    [Fact]
    public void ExistingSettingsRetainCurrentSoundLevel()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"Hotkey\":{\"Modifiers\":6,\"Key\":86},\"CopySound\":true}")!;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(settings));
        Assert.True(document.RootElement.TryGetProperty("CopySoundVolume", out var volume));
        Assert.Equal(50, volume.GetInt32());
        Assert.True(settings.CopySound);
    }

    private static byte[] Wave()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(44);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(44100);
        writer.Write(88200);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(8);
        foreach (short sample in new short[]
        {
            1000,
            -1000,
            20000,
            -20000
        }

        )
        {
            writer.Write(sample);
        }

        return stream.ToArray();
    }
}
