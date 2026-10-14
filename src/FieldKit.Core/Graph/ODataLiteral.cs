namespace FieldKit.Core.Graph;

public static class ODataLiteral
{
    /// <summary>Quotes a value for use inside an OData <c>$filter</c> string literal (<c>'</c> becomes <c>''</c>).</summary>
    public static string String(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>
    /// Cleans free text for a <c>$search</c> clause: drops quotes and backslashes (which would
    /// break the clause) and collapses whitespace. Null when fewer than two characters remain.
    /// </summary>
    public static string? SearchTerm(string text)
    {
        var kept = new string(text.Where(character => character is not ('"' or '\\')).ToArray());
        var cleaned = string.Join(' ', kept.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length >= 2 ? cleaned : null;
    }
}
