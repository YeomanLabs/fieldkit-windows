namespace FieldKit.Core.Domain;

public sealed record SiteWalkCsvRow
{
    public required string Site { get; init; }

    public string Area { get; init; } = "";

    public required DateTimeOffset ScannedAt { get; init; }

    public required string Serial { get; init; }

    public string? IntuneDeviceName { get; init; }

    public string? EntraDisplayName { get; init; }

    public string? PrimaryUser { get; init; }

    public DateTimeOffset? LastSync { get; init; }

    public string? Compliance { get; init; }

    public string? TrustType { get; init; }

    public string? AutopilotGroupTag { get; init; }

    public IReadOnlyList<string> Flags { get; init; } = [];
}
