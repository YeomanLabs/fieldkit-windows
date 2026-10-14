using System.Globalization;
using System.Text;

namespace FieldKit.Core.Domain;

/// <summary>
/// Builds the site walk export. Output opens cleanly in Excel: UTF-8 BOM, CRLF line endings,
/// dates Excel recognises, and cells that could run as formulas are neutralised.
/// </summary>
public static class SiteWalkCsv
{
    public static readonly IReadOnlyList<string> Header =
    [
        "site", "area", "scannedAt", "serial", "intuneDeviceName", "entraDisplayName",
        "primaryUser", "lastSync", "compliance", "trustType", "autopilotGroupTag", "flags",
    ];

    public static string Make(IEnumerable<SiteWalkCsvRow> rows, TimeZoneInfo? timeZone = null)
    {
        var zone = timeZone ?? TimeZoneInfo.Local;
        string Date(DateTimeOffset? date) =>
            date is { } value
                ? TimeZoneInfo.ConvertTime(value, zone).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : "";

        var lines = new List<IReadOnlyList<string>> { Header };
        lines.AddRange(rows.Select(row => (IReadOnlyList<string>)
        [
            row.Site,
            row.Area,
            Date(row.ScannedAt),
            row.Serial,
            row.IntuneDeviceName ?? "",
            row.EntraDisplayName ?? "",
            row.PrimaryUser ?? "",
            Date(row.LastSync),
            row.Compliance ?? "",
            row.TrustType ?? "",
            row.AutopilotGroupTag ?? "",
            string.Join("; ", row.Flags),
        ]));

        var csv = new StringBuilder("﻿");
        foreach (var line in lines)
        {
            csv.Append(string.Join(',', line.Select(Escape))).Append("\r\n");
        }
        return csv.ToString();
    }

    public static string Escape(string field)
    {
        var value = field;
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        {
            value = "'" + value;
        }
        var needsQuotes = value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            || (value.Length > 0 && (value[0] == ' ' || value[^1] == ' '));
        if (!needsQuotes) return value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
