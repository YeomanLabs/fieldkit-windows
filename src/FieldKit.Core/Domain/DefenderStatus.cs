namespace FieldKit.Core.Domain;

/// <summary>Microsoft Defender state for a Windows device.</summary>
public sealed record DefenderStatus
{
    /// <summary>Signatures older than this are worth flagging in the UI.</summary>
    public const int StaleSignatureDays = 3;

    public bool? RealTimeProtectionEnabled { get; init; }

    public bool? MalwareProtectionEnabled { get; init; }

    public bool? TamperProtectionEnabled { get; init; }

    public bool? NetworkInspectionEnabled { get; init; }

    public string? SignatureVersion { get; init; }

    public DateTimeOffset? SignatureUpdatedAt { get; init; }

    public DateTimeOffset? LastQuickScanAt { get; init; }

    public DateTimeOffset? LastFullScanAt { get; init; }

    public bool? RebootRequired { get; init; }

    public bool? FullScanRequired { get; init; }

    public string? DeviceState { get; init; }

    public string? EngineVersion { get; init; }

    public string? ProductVersion { get; init; }

    public bool SignaturesAreStale(DateTimeOffset? now = null)
    {
        if (SignatureUpdatedAt is not { } updated) return false;
        return (now ?? DateTimeOffset.Now) - updated > TimeSpan.FromDays(StaleSignatureDays);
    }
}
