using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// The reads behind "Why didn't it install?" for one app on one device (Windows only, 2026-10-16).
/// The app's assignments with their filters, and its relationships, exist only in beta.
/// </summary>
public sealed class AppInstallLookup(IGraphClient client)
{
    /// <summary>The app, its assignments and related apps; for Win32 apps also its rules and requirements.</summary>
    public async Task<AppDetails> AppAsync(string appId, CancellationToken cancellationToken = default)
    {
        var path = $"deviceAppManagement/mobileApps/{appId}";
        var app = await client.GetAsync<MobileAppDto>(GraphRequest.Get(path,
            [("$select", MobileAppDto.BaseSelect), ("$expand", "assignments,relationships")], version: GraphVersion.Beta), cancellationToken).ConfigureAwait(false);
        var win32 = app.IsWin32
            ? await client.GetAsync<MobileAppDto>(GraphRequest.Get(path, [("$select", MobileAppDto.Win32Select)], version: GraphVersion.Beta), cancellationToken).ConfigureAwait(false)
            : null;
        return app.ToDomain(win32);
    }

    public async Task<AssignmentFilter> FilterAsync(string filterId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"deviceManagement/assignmentFilters/{filterId}", [("$select", AssignmentFilterDto.SelectFields)], version: GraphVersion.Beta);
        return (await client.GetAsync<AssignmentFilterDto>(request, cancellationToken).ConfigureAwait(false)).ToDomain();
    }

    /// <summary>The device's row in the app's install report, in any state.</summary>
    public Task<AppInstallFailure?> ReportAsync(string appId, IntuneDevice device, CancellationToken cancellationToken = default) =>
        new InstallStatusLookup(client).ReportRowAsync(appId, device, cancellationToken: cancellationToken);

    /// <summary>Groups (with nested ones) of "devices/{object ID}" or "users/{UPN}".</summary>
    public Task<IReadOnlyList<DirectoryGroup>> GroupsAsync(string objectPath, CancellationToken cancellationToken = default) =>
        new DirectoryLookup(client).GroupsOfAsync(objectPath, cancellationToken: cancellationToken);

    /// <summary>Architecture and memory, which v1.0 doesn't report.</summary>
    public Task<HardwareDetails> HardwareAsync(string managedDeviceId, CancellationToken cancellationToken = default) =>
        new DeviceInventoryLookup(client).HardwareAsync(managedDeviceId, cancellationToken);
}
