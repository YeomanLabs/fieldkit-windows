namespace FieldKit.Core.Domain;

/// <summary>What a site walk keeps about a device at scan time. Device detail always fetches live.</summary>
public sealed record DeviceSnapshot
{
    public required string Serial { get; init; }

    public string? IntuneDeviceName { get; init; }

    public string? EntraDisplayName { get; init; }

    public string? PrimaryUser { get; init; }

    public DateTimeOffset? LastSync { get; init; }

    public string? Compliance { get; init; }

    public string? TrustType { get; init; }

    public string? AutopilotGroupTag { get; init; }

    public IReadOnlyList<Flag> Flags { get; init; } = [];

    public FlagSeverity? WorstSeverity => Flags.Count == 0 ? null : Flags.Max(flag => flag.Severity);

    /// <summary>Name to show in lists: Intune first, then Entra.</summary>
    public string? DisplayName => IntuneDeviceName ?? EntraDisplayName;
}
