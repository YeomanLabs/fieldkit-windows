using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary><c>GET /deviceManagement/managedDevices</c> item.</summary>
internal sealed class ManagedDeviceDto
{
    public static readonly string SelectFields = string.Join(',',
        "id", "deviceName", "serialNumber", "userId", "userPrincipalName", "userDisplayName",
        "lastSyncDateTime", "complianceState", "operatingSystem", "osVersion", "model",
        "manufacturer", "managedDeviceOwnerType", "azureADDeviceId", "enrolledDateTime",
        "isEncrypted", "totalStorageSpaceInBytes", "freeStorageSpaceInBytes");

    public required string Id { get; init; }

    public string? DeviceName { get; init; }

    public string? SerialNumber { get; init; }

    public string? UserId { get; init; }

    public string? UserPrincipalName { get; init; }

    public string? UserDisplayName { get; init; }

    public DateTimeOffset? LastSyncDateTime { get; init; }

    public string? ComplianceState { get; init; }

    public string? OperatingSystem { get; init; }

    public string? OsVersion { get; init; }

    public string? Model { get; init; }

    public string? Manufacturer { get; init; }

    public string? ManagedDeviceOwnerType { get; init; }

    public string? AzureADDeviceId { get; init; }

    public DateTimeOffset? EnrolledDateTime { get; init; }

    public bool? IsEncrypted { get; init; }

    public long? TotalStorageSpaceInBytes { get; init; }

    public long? FreeStorageSpaceInBytes { get; init; }

    public IntuneDevice ToDomain() =>
        new()
        {
            Id = Id,
            DeviceName = DeviceName ?? "",
            SerialNumber = SerialNumber ?? "",
            UserId = UserId ?? "",
            UserPrincipalName = UserPrincipalName ?? "",
            UserDisplayName = UserDisplayName ?? "",
            LastSyncDateTime = GraphJson.Meaningful(LastSyncDateTime),
            ComplianceState = Domain.ComplianceState.FromRaw(ComplianceState),
            OperatingSystem = OperatingSystem ?? "",
            OsVersion = OsVersion ?? "",
            Model = Model ?? "",
            Manufacturer = Manufacturer ?? "",
            OwnerType = ManagedDeviceOwnerType ?? "unknown",
            AzureADDeviceId = AzureADDeviceId,
            EnrolledDateTime = GraphJson.Meaningful(EnrolledDateTime),
            IsEncrypted = IsEncrypted,
            Storage = StorageUsage.Create(TotalStorageSpaceInBytes, FreeStorageSpaceInBytes),
        };
}
