using FieldKit.Core.Domain;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>A finished (or in-progress) walk's summary and CSV export (ports <c>SiteWalkSummaryView</c>).</summary>
public sealed class SiteWalkSummaryViewModel
{
    public SiteWalkSummaryViewModel(SiteWalk walk, ILocalStore store)
    {
        Walk = store.SiteWalk(walk.Id) ?? walk;
        Scans = store.Scans(walk.Id);
    }

    public SiteWalk Walk { get; }

    public IReadOnlyList<ScanRecord> Scans { get; }

    public IReadOnlyList<InfoRow> Details
    {
        get
        {
            var rows = new List<InfoRow> { new("Site", Walk.Site) };
            if (Walk.Area.Length > 0) rows.Add(new InfoRow("Area", Walk.Area));
            rows.Add(new InfoRow("Started", DateText.Absolute(Walk.StartedAt)));
            rows.Add(new InfoRow("Ended", Walk.EndedAt is { } ended ? DateText.Absolute(ended) : "In progress"));
            rows.Add(new InfoRow("Devices scanned", Count(Scans.Count)));
            if (FailedCount > 0) rows.Add(new InfoRow("Lookups failed", Count(FailedCount), InfoTone.Caution));
            if (PendingCount > 0) rows.Add(new InfoRow("Still looking up", Count(PendingCount)));
            return rows;
        }
    }

    public int FailedCount => Scans.Count(scan => scan.Status == ScanStatus.Failed);

    public int PendingCount => Scans.Count(scan => scan.Status == ScanStatus.Pending);

    /// <summary>How many devices had each flag, in flag order.</summary>
    public IReadOnlyList<InfoRow> FlagCounts
    {
        get
        {
            var all = Scans.SelectMany(scan => scan.FlagKinds).ToList();
            return Enum.GetValues<FlagKind>()
                .Select(kind => (kind, count: all.Count(found => found == kind)))
                .Where(entry => entry.count > 0)
                .Select(entry => new InfoRow(entry.kind.Title(), Count(entry.count)))
                .ToList();
        }
    }

    public bool NoFlags => FlagCounts.Count == 0;

    public bool HasFlags => !NoFlags;

    public string FileName => SiteWalkExporter.FileName(Walk);

    public string Csv() => SiteWalkExporter.Csv(Walk, Scans);

    private static string Count(int value) => value.ToString(System.Globalization.CultureInfo.CurrentCulture);
}
