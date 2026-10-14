namespace FieldKit.Core.Domain;

/// <summary>Turns a raw barcode payload or typed text into a lookup-ready serial, or null if it isn't one.</summary>
public static class SerialNormalizer
{
    public const int MinLength = 5;
    public const int MaxLength = 30;

    /// <summary>
    /// Label prefixes, longest first. Prefixes made only of letters (<c>SN</c>, <c>SERIAL</c>) are
    /// stripped only when a separator follows, so a serial that happens to start with
    /// "SN" survives.
    /// </summary>
    private static readonly string[] Prefixes =
    [
        "SERIAL NUMBER", "SERIAL NO.", "SERIAL NO", "SERIAL", "S/N", "SN", "(S)",
    ];

    private static readonly char[] Separators = [':', ' ', '#', '.', '-'];

    private static readonly HashSet<string> UrlSerialKeys =
    [
        "sn", "serial", "serialnumber", "serial_number", "serialno",
    ];

    public static string? Normalize(string raw)
    {
        var value = raw.Trim();
        if (SerialFromUrl(value) is { } fromUrl)
        {
            value = fromUrl.Trim();
        }
        value = value.ToUpperInvariant();
        // Code 39 start/stop characters.
        value = value.Trim('*');
        value = StripPrefix(value);

        if (value.Length is < MinLength or > MaxLength || !value.All(IsSerialCharacter)) return null;
        return value;
    }

    private static string? SerialFromUrl(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }
        var query = uri.Query.TrimStart('?');
        if (query.Length == 0) return null;
        foreach (var item in query.Split('&'))
        {
            var separator = item.IndexOf('=');
            var name = Uri.UnescapeDataString(separator < 0 ? item : item[..separator]);
            if (!UrlSerialKeys.Contains(name.ToLowerInvariant())) continue;
            return separator < 0 ? null : Uri.UnescapeDataString(item[(separator + 1)..]);
        }
        return null;
    }

    private static string StripPrefix(string value)
    {
        foreach (var prefix in Prefixes)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = value[prefix.Length..];
            var lettersOnly = prefix.All(char.IsLetter);
            if (lettersOnly && rest.Length > 0 && !Separators.Contains(rest[0])) continue;
            return rest.TrimStart(Separators);
        }
        return value;
    }

    private static bool IsSerialCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character == '-';
}
