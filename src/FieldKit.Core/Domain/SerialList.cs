namespace FieldKit.Core.Domain;

/// <summary>
/// A pasted list of serials (Windows only): one per line, or separated by commas or tabs, as
/// when a column is copied from Excel. Each entry is normalised; duplicates keep their first place.
/// </summary>
public sealed record SerialList(IReadOnlyList<string> Serials, IReadOnlyList<string> Invalid)
{
    private static readonly char[] Separators = ['\r', '\n', '\t', ',', ';'];

    public static SerialList Parse(string text)
    {
        var serials = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var invalid = new List<string>();
        foreach (var entry in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (SerialNormalizer.Normalize(entry) is not { } serial)
            {
                invalid.Add(entry);
            }
            else if (seen.Add(serial))
            {
                serials.Add(serial);
            }
        }
        return new SerialList(serials, invalid);
    }
}
