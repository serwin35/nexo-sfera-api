namespace NexoSferaApi.Helpers;

/// <summary>
/// Guarded access to a single member of a compiled SDK entity. Member names are compile-time checked by the caller's
/// lambda; this only turns an SDK runtime exception on one member (lazy load failure, detached proxy, ...) into
/// the given fallback so that one bad member never fails the whole response.
/// </summary>
internal static class SdkMember
{
    public static T Read<T>(Func<T> getter, T fallback)
    {
        try
        {
            return getter();
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>Trimmed string, or <c>null</c> when empty.</summary>
    public static string? Text(Func<string?> getter)
    {
        var value = Read(getter, null);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
