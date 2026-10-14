namespace FieldKit.Core.ViewModels;

/// <summary>A titled group of rows. Built only from values Intune reported, so a section with nothing to show isn't listed.</summary>
public sealed record InfoSection(string Title, IReadOnlyList<InfoRow> Rows)
{
    /// <summary>Keeps the rows that have a value, as iOS's <c>rows(_:_:)</c> does.</summary>
    public static InfoSection? From(string title, params (string Label, string? Value)[] items)
    {
        var rows = items
            .Where(item => !string.IsNullOrEmpty(item.Value))
            .Select(item => new InfoRow(item.Label, item.Value!))
            .ToList();
        return rows.Count == 0 ? null : new InfoSection(title, rows);
    }
}
