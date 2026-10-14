using System.Globalization;
using System.Text;

namespace FieldKit.Core.Domain;

public static class GroupTag
{
    public const int MaxLength = 100;

    /// <summary>
    /// Trims a typed Autopilot group tag; null if it's too long or contains control or format
    /// characters. Anything else is left for Graph to judge. An empty tag clears the tag.
    /// </summary>
    public static string? Normalized(string text)
    {
        var tag = text.Trim();
        if (new StringInfo(tag).LengthInTextElements > MaxLength) return null;
        foreach (var rune in tag.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format) return null;
        }
        return tag;
    }
}
