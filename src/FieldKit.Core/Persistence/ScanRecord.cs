using FieldKit.Core.Domain;

namespace FieldKit.Core.Persistence;

/// <summary>A snapshot of a device at scan time, kept for the walk's summary and CSV (ports <c>ScanRecord</c>).</summary>
/// <param name="Flags">FlagKind raw values.</param>
/// <param name="WorstSeverity">FlagSeverity raw value, or null with no flags.</param>
public sealed record ScanRecord(
    long Id,
    string WalkId,
    string Serial,
    DateTimeOffset ScannedAt,
    ScanStatus Status,
    string? FailureMessage = null,
    string? DeviceName = null,
    string? EntraDisplayName = null,
    string? PrimaryUser = null,
    DateTimeOffset? LastSync = null,
    string? Compliance = null,
    string? TrustType = null,
    string? AutopilotGroupTag = null,
    IReadOnlyList<string>? Flags = null,
    string? WorstSeverity = null)
{
    public IReadOnlyList<string> FlagValues => Flags ?? [];

    public IReadOnlyList<FlagKind> FlagKinds =>
        FlagValues.Select(FlagKindExtensions.FromRawValue).OfType<FlagKind>().ToList();

    public FlagSeverity? Severity => WorstSeverity is { } raw ? FlagSeverityExtensions.FromRawValue(raw) : null;

    /// <summary>Name to show: Intune first, then Entra.</summary>
    public string? DisplayName => DeviceName ?? EntraDisplayName;

    public ScanRecord Applying(DeviceSnapshot snapshot) =>
        this with
        {
            Status = ScanStatus.Done,
            FailureMessage = null,
            DeviceName = snapshot.IntuneDeviceName,
            EntraDisplayName = snapshot.EntraDisplayName,
            PrimaryUser = snapshot.PrimaryUser,
            LastSync = snapshot.LastSync,
            Compliance = snapshot.Compliance,
            TrustType = snapshot.TrustType,
            AutopilotGroupTag = snapshot.AutopilotGroupTag,
            Flags = snapshot.Flags.Select(flag => flag.Kind.RawValue()).ToList(),
            WorstSeverity = snapshot.WorstSeverity?.RawValue(),
        };

    public ScanRecord Failing(string message) => this with { Status = ScanStatus.Failed, FailureMessage = message };
}
