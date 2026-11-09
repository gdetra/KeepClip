using KeepClip.Application;
using KeepClip.Application.Paste;

namespace KeepClip.App.Shelf;

public sealed class PasteClipFactory(
    IClipboardAdapter clipboard,
    IWindowTarget target,
    IPasteInput input,
    IPasteContentLoader contentLoader)
{
    public PasteClip Create(Action hide) => new(new PasteCoordinator(clipboard, target, input, hide), contentLoader);
}
