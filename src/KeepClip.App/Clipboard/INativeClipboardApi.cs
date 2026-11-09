namespace KeepClip.App.Clipboard;

internal interface INativeClipboardApi
{
    uint Sequence { get; }

    uint RegisterFormat(string name);
    bool Contains(uint format);
    bool Open(nint owner);
    void Close();
    bool Empty();
    nint Allocate(byte[] bytes);
    void Free(nint handle);
    bool Publish(uint format, nint handle);
    byte[]? Read(uint format, int maxBytes);
    IReadOnlyList<string> ReadFiles();
}
