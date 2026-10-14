using FieldKit.Core.Persistence;

namespace FieldKit.Core.ViewModels;

/// <summary>A walk in the history list (ports <c>SiteWalkHistoryView</c>'s rows).</summary>
public sealed record SiteWalkItem(SiteWalk Walk, int ScanCount)
{
    public string Site => Walk.Site;

    public bool IsActive => Walk.IsActive;

    /// <summary>"Floor 2 · Oct 12, 2026 at 2:22 PM · 14 devices".</summary>
    public string Subtitle => DeviceText.Join(" · ", Walk.Area, DateText.Absolute(Walk.StartedAt), ScanCount == 1 ? "1 device" : $"{ScanCount} devices");

    public string AutomationName => DeviceText.Join(", ", Site, IsActive ? "in progress" : "", Subtitle);
}
