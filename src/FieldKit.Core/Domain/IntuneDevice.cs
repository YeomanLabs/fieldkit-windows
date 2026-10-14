namespace FieldKit.Core.Domain;

public sealed record IntuneDevice
{
    /// <summary>Always set by Graph; defaults only so XAML can create the type.</summary>
    public string Id { get; init; } = "";

    public string DeviceName { get; init; } = "";

    public string SerialNumber { get; init; } = "";

    /// <summary>The primary user's Entra object ID; empty when the device has none.</summary>
    public string UserId { get; init; } = "";

    public string UserPrincipalName { get; init; } = "";

    public string UserDisplayName { get; init; } = "";

    public DateTimeOffset? LastSyncDateTime { get; init; }

    public ComplianceState ComplianceState { get; init; } = ComplianceState.Unknown;

    public string OperatingSystem { get; init; } = "";

    public string OsVersion { get; init; } = "";

    public string Model { get; init; } = "";

    public string Manufacturer { get; init; } = "";

    /// <summary>Graph's <c>managedDeviceOwnerType</c>: <c>company</c>, <c>personal</c> or <c>unknown</c>.</summary>
    public string OwnerType { get; init; } = "unknown";

    public string? AzureADDeviceId { get; init; }

    public DateTimeOffset? EnrolledDateTime { get; init; }

    public bool? IsEncrypted { get; init; }

    public StorageUsage? Storage { get; init; }

    /// <summary>False when Graph returns no ID or the all-zeros GUID it uses for "none".</summary>
    public bool HasEntraDeviceId
    {
        get
        {
            var id = AzureADDeviceId?.Trim();
            return !string.IsNullOrEmpty(id) && id.Any(character => character is not ('0' or '-'));
        }
    }

    public string OwnershipDisplayName =>
        OwnerType.ToLowerInvariant() switch
        {
            "company" => "Corporate",
            "personal" => "Personal",
            _ => "Unknown",
        };
}
