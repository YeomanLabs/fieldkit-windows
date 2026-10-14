using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>v1.0 <c>windowsProtectionState</c>.</summary>
internal sealed class DefenderStatusDto
{
    public bool? RealTimeProtectionEnabled { get; init; }

    public bool? MalwareProtectionEnabled { get; init; }

    public bool? TamperProtectionEnabled { get; init; }

    public bool? NetworkInspectionSystemEnabled { get; init; }

    public string? SignatureVersion { get; init; }

    public bool? SignatureUpdateOverdue { get; init; }

    public DateTimeOffset? LastReportedDateTime { get; init; }

    public DateTimeOffset? LastQuickScanDateTime { get; init; }

    public DateTimeOffset? LastFullScanDateTime { get; init; }

    public bool? RebootRequired { get; init; }

    public bool? FullScanRequired { get; init; }

    public string? DeviceState { get; init; }

    public string? EngineVersion { get; init; }

    public string? AntiMalwareVersion { get; init; }

    public string? ProductStatus { get; init; }

    public string? LastQuickScanSignatureVersion { get; init; }

    public DefenderStatus ToDomain() =>
        new()
        {
            RealTimeProtectionEnabled = RealTimeProtectionEnabled,
            MalwareProtectionEnabled = MalwareProtectionEnabled,
            TamperProtectionEnabled = TamperProtectionEnabled,
            NetworkInspectionEnabled = NetworkInspectionSystemEnabled,
            SignatureVersion = SignatureVersion.NonEmpty(),
            SignatureUpdatedAt = GraphJson.Meaningful(LastReportedDateTime),
            LastQuickScanAt = GraphJson.Meaningful(LastQuickScanDateTime),
            LastFullScanAt = GraphJson.Meaningful(LastFullScanDateTime),
            RebootRequired = RebootRequired,
            FullScanRequired = FullScanRequired,
            DeviceState = DeviceState.NonEmpty(),
            EngineVersion = EngineVersion.NonEmpty(),
            ProductVersion = AntiMalwareVersion.NonEmpty(),
        };
}
