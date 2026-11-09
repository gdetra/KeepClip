namespace KeepClip.Application;

public sealed record ClipboardSourceContext(uint Sequence, uint OwnerProcess, uint ForegroundProcess);
