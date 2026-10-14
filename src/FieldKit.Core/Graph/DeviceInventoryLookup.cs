using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>Inventory for one Intune device. Each call backs its own section and fails alone.</summary>
public sealed class DeviceInventoryLookup(IGraphClient client)
{
    public async Task<DeviceInventory> InventoryAsync(string managedDeviceId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"deviceManagement/managedDevices/{managedDeviceId}",
            [("$select", DeviceInventoryDto.SelectFields)]);
        return (await client.GetAsync<DeviceInventoryDto>(request, cancellationToken).ConfigureAwait(false)).ToDomain();
    }

    /// <summary>Beta only: v1.0 has no <c>hardwareInformation</c>.</summary>
    public async Task<HardwareDetails> HardwareAsync(string managedDeviceId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"deviceManagement/managedDevices/{managedDeviceId}",
            [("$select", HardwareDetailsDto.SelectFields)], version: GraphVersion.Beta);
        return (await client.GetAsync<HardwareDetailsDto>(request, cancellationToken).ConfigureAwait(false)).ToDomain();
    }

    /// <summary>Windows only. Null when Defender hasn't reported.</summary>
    public async Task<DefenderStatus?> DefenderAsync(string managedDeviceId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"deviceManagement/managedDevices/{managedDeviceId}/windowsProtectionState");
        try
        {
            return (await client.GetAsync<DefenderStatusDto>(request, cancellationToken).ConfigureAwait(false)).ToDomain();
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.NotFound)
        {
            return null;
        }
    }

    /// <summary>Beta only. Apps inventory found on the device, sorted by name.</summary>
    public async Task<IReadOnlyList<DetectedApp>> DetectedAppsAsync(string managedDeviceId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"deviceManagement/managedDevices/{managedDeviceId}/detectedApps",
            [("$select", "id,displayName,version,publisher,sizeInByte")], version: GraphVersion.Beta);
        var items = await client.GetAllAsync<DetectedAppDto>(request, maxPages: 20, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToDomain()).OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
