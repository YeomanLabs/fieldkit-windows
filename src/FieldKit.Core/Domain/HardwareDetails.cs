namespace FieldKit.Core.Domain;

/// <summary>
/// Hardware and network details Intune collects for a device. Every field is optional:
/// what's reported depends on the platform and when inventory last ran.
/// </summary>
public sealed record HardwareDetails
{
    public long? PhysicalMemoryBytes { get; init; }

    public string? ProcessorArchitecture { get; init; }

    public string? OperatingSystemEdition { get; init; }

    public string? OsBuildNumber { get; init; }

    public string? ProductName { get; init; }

    public string? BiosVersion { get; init; }

    public string? TpmVersion { get; init; }

    public string? TpmManufacturer { get; init; }

    public int? BatteryHealthPercentage { get; init; }

    public int? BatteryChargeCycles { get; init; }

    public double? BatteryLevelPercentage { get; init; }

    public string? IpAddress { get; init; }

    public string? Subnet { get; init; }

    public IReadOnlyList<string> WiredIpAddresses { get; init; } = [];

    public string? FullyQualifiedName { get; init; }

    public string? Carrier { get; init; }

    public string? CellularTechnology { get; init; }

    public string? Language { get; init; }

    public bool HasBattery =>
        BatteryHealthPercentage is not null || BatteryChargeCycles is not null || BatteryLevelPercentage is not null;
}
