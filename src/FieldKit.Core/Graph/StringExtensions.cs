namespace FieldKit.Core.Graph;

internal static class StringExtensions
{
    /// <summary>Null for missing, empty or whitespace-only strings; otherwise trimmed.</summary>
    public static string? NonEmpty(this string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Null for an empty string; anything else unchanged.</summary>
    public static string? NullIfEmpty(this string? value) => string.IsNullOrEmpty(value) ? null : value;
}
