namespace FieldKit.Core.Domain;

/// <summary>Everything FieldKit found about one app on one device. Each part is null when it couldn't be read.</summary>
public sealed record AppInstallEvidence
{
    public required AppInstallStatus App { get; init; }

    public required AppDeviceFacts Device { get; init; }

    /// <summary>The device's row in Intune's install report: error code, Intune's words, when.</summary>
    public AppInstallFailure? Report { get; init; }

    public AppDetails? Details { get; init; }

    public AppTargeting? Targeting { get; init; }

    /// <summary>What the device's own Intune Management Extension log says, once it's been read.</summary>
    public AppLogReport? Log { get; init; }

    /// <summary>Every app on the device's install status tab, to see how its dependencies are doing.</summary>
    public IReadOnlyList<AppInstallStatus> DeviceApps { get; init; } = [];

    public DateTimeOffset Now { get; init; }
}
