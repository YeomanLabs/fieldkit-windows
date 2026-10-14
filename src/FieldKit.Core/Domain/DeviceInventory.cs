namespace FieldKit.Core.Domain;

/// <summary>Identity, storage and connectivity details from the Intune record.</summary>
public sealed record DeviceInventory
{
    public StorageUsage? Storage { get; init; }

    public string? WifiMac { get; init; }

    public string? EthernetMac { get; init; }

    public string? Imei { get; init; }

    public string? PhoneNumber { get; init; }

    public bool? IsSupervised { get; init; }

    public string? JailBroken { get; init; }

    public string? ManagementAgent { get; init; }

    public string? EnrollmentType { get; init; }
}
