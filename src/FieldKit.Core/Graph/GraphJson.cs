using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace FieldKit.Core.Graph;

/// <summary>Reading Graph responses: body normalisation, dates, and decoding errors safe to show.</summary>
public static partial class GraphJson
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mm:ssK"];

    /// <summary>
    /// Strips a UTF-8 byte-order mark and converts UTF-16 bodies to UTF-8. Some Entra
    /// endpoints answer this way, and the JSON reader rejects both.
    /// </summary>
    public static byte[] Normalized(byte[] data)
    {
        if (data is [0xEF, 0xBB, 0xBF, ..]) return data[3..];
        if (data.Length < 2) return data;

        var (encoding, start) = (data[0], data[1]) switch
        {
            (0xFF, 0xFE) => (Utf16(bigEndian: false), 2),
            (0xFE, 0xFF) => (Utf16(bigEndian: true), 2),
            (not 0, 0) => (Utf16(bigEndian: false), 0),
            (0, not 0) => (Utf16(bigEndian: true), 0),
            _ => ((Encoding?)null, 0),
        };
        if (encoding is null) return data;
        try
        {
            return Encoding.UTF8.GetBytes(encoding.GetString(data, start, data.Length - start));
        }
        catch (ArgumentException)
        {
            return data;
        }
    }

    /// <summary>
    /// ISO 8601, with or without fractional seconds (Graph sends up to seven digits) and
    /// occasionally without a time zone (taken as UTC). Null when it can't be read.
    /// </summary>
    public static DateTimeOffset? ParseDate(string text)
    {
        var value = text.Trim();
        if (value.Length == 0) return null;
        if (!value.Contains('T', StringComparison.Ordinal)) value += "T00:00:00";
        if (!HasTimeZone(value)) value += "Z";
        // .NET reads up to seven fractional digits; drop any beyond.
        value = ExtraFractionDigits().Replace(value, "$1");
        return DateTimeOffset.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    /// <summary>Graph uses 0001-01-01T00:00:00Z for "never"; anything up to 1970 counts as no date.</summary>
    public static DateTimeOffset? Meaningful(DateTimeOffset? date) =>
        date is { } value && value > DateTimeOffset.UnixEpoch ? value : null;

    /// <summary>Where decoding failed, by field path only, so it's safe to show and log.</summary>
    public static string Describe(JsonException error)
    {
        var path = ReadablePath(error.Path);
        var message = error.Message;
        if (MissingProperties().Match(message) is { Success: true } missing)
        {
            var name = missing.Groups[1].Value;
            return $"missing {(path == "top level" ? name : $"{path}.{name}")}";
        }
        if (message.Contains("null", StringComparison.OrdinalIgnoreCase) && error.InnerException is null
            && message.Contains("doesn't allow", StringComparison.OrdinalIgnoreCase))
        {
            return $"empty {path}";
        }
        if (message.StartsWith("The JSON value could not be converted", StringComparison.Ordinal))
        {
            return $"unexpected type at {path}";
        }
        return $"unreadable {path}";
    }

    internal static T Decode<T>(byte[] data)
    {
        try
        {
            return JsonSerializer.Deserialize(Normalized(data), TypeInfo<T>()) ?? throw GraphException.Decoding("empty top level");
        }
        catch (JsonException error)
        {
            var detail = Describe(error);
            System.Diagnostics.Debug.WriteLine($"Decoding {typeof(T).Name} failed: {detail}");
            throw GraphException.Decoding(detail);
        }
        catch (Exception error) when (error is NotSupportedException or ArgumentException or FormatException)
        {
            throw GraphException.Decoding("unreadable response");
        }
    }

    private static JsonTypeInfo<T> TypeInfo<T>() =>
        GraphJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
        ?? throw new InvalidOperationException($"{typeof(T).Name} isn't registered in GraphJsonContext.");

    private static Encoding Utf16(bool bigEndian) =>
        new UnicodeEncoding(bigEndian, byteOrderMark: false, throwOnInvalidBytes: true);

    private static bool HasTimeZone(string value)
    {
        var timeStart = value.IndexOf('T', StringComparison.Ordinal);
        if (timeStart < 0) return false;
        var time = value[timeStart..];
        return time.EndsWith('Z') || time.EndsWith('z')
            || time.Contains('+', StringComparison.Ordinal)
            || time[1..].Contains('-', StringComparison.Ordinal);
    }

    /// <summary>"$.value[0].id" → "value.0.id", matching the iOS field paths.</summary>
    private static string ReadablePath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path == "$") return "top level";
        var readable = path.TrimStart('$').TrimStart('.').Replace("[", ".", StringComparison.Ordinal).Replace("]", "", StringComparison.Ordinal);
        readable = readable.Replace("'", "", StringComparison.Ordinal).Trim('.');
        return readable.Length == 0 ? "top level" : readable;
    }

    [GeneratedRegex(@"(\.\d{7})\d+")]
    private static partial Regex ExtraFractionDigits();

    [GeneratedRegex(@"missing required properties[^']*'([^']+)'")]
    private static partial Regex MissingProperties();
}
