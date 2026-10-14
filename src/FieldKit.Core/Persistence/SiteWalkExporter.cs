using System.Globalization;
using System.Text;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Persistence;

/// <summary>The site walk CSV (ports <c>SiteWalkExporter</c>): rows in scan order, written as UTF-8 with a BOM and CRLF.</summary>
public static class SiteWalkExporter
{
    public static IReadOnlyList<SiteWalkCsvRow> Rows(SiteWalk walk, IEnumerable<ScanRecord> scans) =>
        scans
            .OrderBy(scan => scan.ScannedAt)
            .Select(scan => new SiteWalkCsvRow
            {
                Site = walk.Site,
                Area = walk.Area,
                ScannedAt = scan.ScannedAt,
                Serial = scan.Serial,
                IntuneDeviceName = scan.DeviceName,
                EntraDisplayName = scan.EntraDisplayName,
                PrimaryUser = scan.PrimaryUser,
                LastSync = scan.LastSync,
                Compliance = scan.Compliance,
                TrustType = scan.TrustType,
                AutopilotGroupTag = scan.AutopilotGroupTag,
                Flags = scan.Status == ScanStatus.Done ? scan.FlagValues : ["lookupFailed"],
            })
            .ToList();

    public static string Csv(SiteWalk walk, IEnumerable<ScanRecord> scans, TimeZoneInfo? timeZone = null) =>
        SiteWalkCsv.Make(Rows(walk, scans), timeZone);

    /// <summary>
    /// "FieldKit-Contoso-HQ-2026-10-12.csv" (SPEC-windows §9; iOS adds the time). The site's
    /// letters and digits are kept, joined by dashes.
    /// </summary>
    public static string FileName(SiteWalk walk, TimeZoneInfo? timeZone = null)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (var character in walk.Site)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(character);
            }
            else if (current.Length > 0)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0) parts.Add(current.ToString());
        var site = parts.Count == 0 ? "site" : string.Join('-', parts);
        var date = TimeZoneInfo.ConvertTime(walk.StartedAt, timeZone ?? TimeZoneInfo.Local).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"FieldKit-{site}-{date}.csv";
    }

    /// <summary>The CSV already starts with the BOM character, so the encoder mustn't add another.</summary>
    public static Encoding FileEncoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
