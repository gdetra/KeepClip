namespace KeepClip.Storage;

public sealed class HistoryStorageException(
    string path,
    Exception inner) : IOException($"Impossibile accedere alla cronologia: {path}. I file disponibili sono stati preservati.", inner);
