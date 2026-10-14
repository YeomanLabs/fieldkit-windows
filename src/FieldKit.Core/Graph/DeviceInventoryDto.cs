using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>v1.0 managed device with the inventory fields selected.</summary>
internal sealed class DeviceInventoryDto
{
    public static readonly string SelectFields = string.Join(',',
        "id", "totalStorageSpaceInBytes", "freeStorageSpaceInBytes", "wiFiMacAddress", "ethernetMacAddress",
        "imei", "phoneNumber", "isSupervised", "jailBroken", "managementAgent", "deviceEnrollmentType");

    public long? TotalStorageSpaceInBytes { get; init; }

    public long? FreeStorageSpaceInBytes { get; init; }

    public string? WiFiMacAddress { get; init; }

    public string? EthernetMacAddress { get; init; }

    public string? Imei { get; init; }

    public string? PhoneNumber { get; init; }

    public bool? IsSupervised { get; init; }

    public string? JailBroken { get; init; }

    public string? ManagementAgent { get; init; }

    public string? DeviceEnrollmentType { get; init; }

    public DeviceInventory ToDomain() =>
        new()
        {
            Storage = StorageUsage.Create(TotalStorageSpaceInBytes, FreeStorageSpaceInBytes),
            WifiMac = Mac(WiFiMacAddress),
            EthernetMac = Mac(EthernetMacAddress),
            Imei = Imei.NonEmpty(),
            PhoneNumber = PhoneNumber.NonEmpty(),
            IsSupervised = IsSupervised,
            JailBroken = JailBroken.NonEmpty(),
            ManagementAgent = ManagementAgent.NonEmpty(),
            EnrollmentType = DeviceEnrollmentType.NonEmpty(),
        };

    /// <summary>Intune sends MACs as bare hex ("A1B2C3D4E5F6"); show them colon-separated.</summary>
    public static string? Mac(string? raw)
    {
        if (raw.NonEmpty() is not { } value) return null;
        var hex = new string(value.Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant();
        if (hex.Length != 12) return value;
        return string.Join(':', Enumerable.Range(0, 6).Select(pair => hex.Substring(pair * 2, 2)));
    }
}
