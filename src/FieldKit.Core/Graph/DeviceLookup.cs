using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// Device lookups by serial. Shared by the device page, site walks, and later features that
/// act on a scanned device.
/// </summary>
public sealed class DeviceLookup(IGraphClient client)
{
    public async Task<IReadOnlyList<IntuneDevice>> IntuneDevicesAsync(string serial, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get("deviceManagement/managedDevices",
        [
            ("$filter", $"serialNumber eq {ODataLiteral.String(serial)}"),
            ("$select", ManagedDeviceDto.SelectFields),
        ]);
        var items = await client.GetAllAsync<ManagedDeviceDto>(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToDomain()).ToList();
    }

    /// <summary>Intune records for an Entra device ID, for opening a device found through a group.</summary>
    public async Task<IReadOnlyList<IntuneDevice>> IntuneDevicesForEntraDeviceAsync(string entraDeviceId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get("deviceManagement/managedDevices",
        [
            ("$filter", $"azureADDeviceId eq {ODataLiteral.String(entraDeviceId)}"),
            ("$select", ManagedDeviceDto.SelectFields),
        ]);
        var items = await client.GetAllAsync<ManagedDeviceDto>(request, maxPages: 1, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToDomain()).ToList();
    }

    /// <summary>Intune devices whose name starts with <paramref name="namePrefix"/> (Graph has no "contains" here).</summary>
    public async Task<IReadOnlyList<IntuneDevice>> SearchDevicesAsync(string namePrefix, int top = 25, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get("deviceManagement/managedDevices",
        [
            ("$filter", $"startswith(deviceName,{ODataLiteral.String(namePrefix)})"),
            ("$select", ManagedDeviceDto.SelectFields),
            ("$top", top.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]);
        var items = await client.GetAllAsync<ManagedDeviceDto>(request, maxPages: 1, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToDomain()).ToList();
    }

    /// <summary>Every Intune device, A–Z by name, a page at a time (the Devices tab).</summary>
    public Task<GraphPage<IntuneDevice>> DevicesPageAsync(Uri? next = null, CancellationToken cancellationToken = default)
    {
        var first = GraphRequest.Get("deviceManagement/managedDevices",
        [
            ("$select", ManagedDeviceDto.SelectFields),
            ("$orderby", "deviceName"),
            ("$top", DirectoryLookup.PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]);
        var request = next is null ? first : GraphClientExtensions.NextPage(next, first);
        return client.GetPageAsync<ManagedDeviceDto, IntuneDevice>(request, dto => dto.ToDomain(), cancellationToken);
    }

    /// <summary>Each compliance policy's state on the device, with per-setting results.</summary>
    public async Task<IReadOnlyList<CompliancePolicyState>> CompliancePoliciesAsync(string managedDeviceId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"deviceManagement/managedDevices/{managedDeviceId}/deviceCompliancePolicyStates");
        var items = await client.GetAllAsync<CompliancePolicyStateDto>(request, maxPages: 2, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToDomain()).ToList();
    }

    /// <summary>Returns null when Entra has no device with this ID.</summary>
    public async Task<EntraDevice?> EntraDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get("devices",
        [
            ("$filter", $"deviceId eq {ODataLiteral.String(deviceId)}"),
            ("$select", EntraDeviceDto.SelectFields),
        ]);
        try
        {
            var items = await client.GetAllAsync<EntraDeviceDto>(request, maxPages: 1, cancellationToken).ConfigureAwait(false);
            return items.FirstOrDefault()?.ToDomain();
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Graph only offers <c>contains</c> here, so results are narrowed to an exact
    /// (case-insensitive) serial match.
    /// </summary>
    public async Task<IReadOnlyList<AutopilotDevice>> AutopilotDevicesAsync(string serial, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get("deviceManagement/windowsAutopilotDeviceIdentities",
            [("$filter", $"contains(serialNumber,{ODataLiteral.String(serial)})")]);
        try
        {
            var items = await client.GetAllAsync<AutopilotDeviceIdentityDto>(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            return items
                .Where(item => string.Equals(item.SerialNumber, serial, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.ToDomain())
                .ToList();
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.NotFound)
        {
            return [];
        }
    }

    /// <summary>
    /// Everything a site walk row needs, with flags. Throws if the Intune lookup fails;
    /// Autopilot or Entra failures only suppress the flags that depend on them.
    /// </summary>
    public async Task<DeviceSnapshot> SnapshotAsync(string serial, AuditSettings settings, CancellationToken cancellationToken = default)
    {
        using var autopilotCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var autopilotTask = AutopilotDevicesAsync(serial, autopilotCancellation.Token);

        IReadOnlyList<IntuneDevice> intune;
        try
        {
            intune = await IntuneDevicesAsync(serial, cancellationToken).ConfigureAwait(false);
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.NotFound)
        {
            intune = [];
        }
        catch
        {
            await autopilotCancellation.CancelAsync().ConfigureAwait(false);
            await Abandon(autopilotTask).ConfigureAwait(false);
            throw;
        }
        var subject = intune.FirstOrDefault();

        var entra = Lookup<EntraDevice>.Skipped;
        if (subject is { HasEntraDeviceId: true, AzureADDeviceId: { } deviceId })
        {
            try
            {
                entra = await EntraDeviceAsync(deviceId, cancellationToken).ConfigureAwait(false) is { } device
                    ? Lookup<EntraDevice>.Found(device)
                    : Lookup<EntraDevice>.NotFound;
            }
            catch (GraphException error) when (error.Kind != GraphErrorKind.Unauthorized)
            {
                entra = Lookup<EntraDevice>.Unavailable;
            }
            catch
            {
                await autopilotCancellation.CancelAsync().ConfigureAwait(false);
                await Abandon(autopilotTask).ConfigureAwait(false);
                throw;
            }
        }

        Lookup<IReadOnlyList<AutopilotDevice>> autopilot;
        try
        {
            var records = await autopilotTask.ConfigureAwait(false);
            autopilot = records.Count == 0
                ? Lookup<IReadOnlyList<AutopilotDevice>>.NotFound
                : Lookup<IReadOnlyList<AutopilotDevice>>.Found(records);
        }
        catch (GraphException error) when (error.Kind != GraphErrorKind.Unauthorized)
        {
            autopilot = Lookup<IReadOnlyList<AutopilotDevice>>.Unavailable;
        }

        var flags = FlagEvaluator.Evaluate(
            intune.Count == 0 ? Lookup<IReadOnlyList<IntuneDevice>>.NotFound : Lookup<IReadOnlyList<IntuneDevice>>.Found(intune),
            entra,
            autopilot,
            settings);
        var entraDevice = entra.State == LookupState.Found ? entra.Value : null;
        var autopilotRecord = autopilot.State == LookupState.Found ? autopilot.Value?.FirstOrDefault() : null;

        return new DeviceSnapshot
        {
            Serial = serial,
            IntuneDeviceName = subject?.DeviceName.NullIfEmpty(),
            EntraDisplayName = entraDevice?.DisplayName.NullIfEmpty(),
            PrimaryUser = subject?.UserPrincipalName.NullIfEmpty(),
            LastSync = subject?.LastSyncDateTime,
            Compliance = subject?.ComplianceState.DisplayName,
            TrustType = entraDevice?.TrustType.DisplayName,
            AutopilotGroupTag = autopilotRecord?.GroupTag.NullIfEmpty(),
            Flags = flags,
        };
    }

    /// <summary>Waits out a lookup whose result no longer matters, so its failure isn't left unobserved.</summary>
    private static async Task Abandon(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception error) when (error is GraphException or OperationCanceledException)
        {
            // Superseded by the error being thrown.
        }
    }
}
