namespace KeepClip.Application.Selection;

public static class SelectionReadSafety
{
    // Unsupported/mixed metadata and provider failures cannot authorize a clipboard write.
    public static bool? ProbeReadOnly(Func<object?> readAttribute)
    {
        try
        {
            return readAttribute() is bool readOnly ? readOnly : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
