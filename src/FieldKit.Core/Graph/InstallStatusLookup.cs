using System.Globalization;
using System.Text.Json;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// Did it install? For one device: the apps targeted at it with their install state, why the
/// failed ones failed, and each configuration policy's result (Windows only, 2026-10-14). All
/// beta: Intune has no v1.0 equivalent. Shapes confirmed against a real tenant on 2026-10-14.
/// </summary>
public sealed class InstallStatusLookup(IGraphClient client)
{
    /// <summary>The user ID Intune expects for a device with no primary user.</summary>
    public const string NoUser = "00000000-0000-0000-0000-000000000000";

    /// <summary>Failed apps looked up for an error code; each is one report request.</summary>
    public const int MaxFailureLookups = 10;

    private const int FailureRows = 1000;
    private const int PolicyPageSize = 50;
    private const int MaxPolicyPages = 10;

    /// <summary>Every app targeted at the device or its primary user, with Intune's install state.</summary>
    public async Task<IReadOnlyList<AppInstallStatus>> AppsAsync(IntuneDevice device, CancellationToken cancellationToken = default)
    {
        var user = device.UserId.Length > 0 ? device.UserId : NoUser;
        var request = GraphRequest.Get($"users('{user}')/mobileAppIntentAndStates('{device.Id}')", version: GraphVersion.Beta);
        try
        {
            var dto = await client.GetAsync<MobileAppIntentAndStateDto>(request, cancellationToken).ConfigureAwait(false);
            return (dto.MobileAppList ?? []).Select(item => item.ToDomain()).ToList();
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.NotFound)
        {
            return [];
        }
    }

    /// <summary>
    /// Why an app failed on this device, from Intune's per-app install report. The report can't
    /// be filtered by device, so it's narrowed to failed installs (and the device name, as a
    /// search) and this device's row is picked out by ID. Null when Intune has no such row.
    /// </summary>
    public async Task<AppInstallFailure?> FailureAsync(AppInstallStatus app, IntuneDevice device, CancellationToken cancellationToken = default)
    {
        var body = ReportBody(
            $"(ApplicationId eq {ODataLiteral.String(app.AppId)}) and (InstallState eq '{app.State.ReportCode().ToString(CultureInfo.InvariantCulture)}')",
            device.DeviceName, 0, FailureRows);
        var table = await client.PostReportAsync(
            GraphRequest.PostJson("deviceManagement/reports/retrieveDeviceAppInstallationStatusReport", body, GraphVersion.Beta), cancellationToken).ConfigureAwait(false);
        for (var row = 0; row < table.Count; row++)
        {
            if (!string.Equals(table.Text(row, "DeviceId"), device.Id, StringComparison.OrdinalIgnoreCase)) continue;
            var code = table.Int(row, "ErrorCode") ?? HexToInt(table.Text(row, "HexErrorCode"));
            return new AppInstallFailure(code, table.Text(row, "AppInstallStateDetails_loc"), table.Date(row, "LastModifiedDateTime"));
        }
        return null;
    }

    /// <summary>Each configuration profile and policy on the device, worst result first.</summary>
    public async Task<IReadOnlyList<PolicyDeploymentStatus>> ConfigurationAsync(IntuneDevice device, CancellationToken cancellationToken = default)
    {
        // The Intune admin center's own request. No orderBy: it makes the report fail (msgraph-beta-sdk-dotnet #1073).
        var filter =
            "((PolicyBaseTypeName eq 'Microsoft.Management.Services.Api.DeviceConfiguration') or (PolicyBaseTypeName eq 'DeviceManagementConfigurationPolicy') " +
            "or (PolicyBaseTypeName eq 'DeviceConfigurationAdmxPolicy') or (PolicyBaseTypeName eq 'Microsoft.Management.Services.Api.DeviceManagementIntent')) " +
            $"and (IntuneDeviceId eq {ODataLiteral.String(device.Id)})";
        string[] select = ["IntuneDeviceId", "PolicyBaseTypeName", "PolicyId", "PolicyStatus", "UPN", "UserId", "PspdpuLastModifiedTimeUtc", "PolicyName", "UnifiedPolicyType"];

        var rows = new List<(string Id, string Name, string Type, PolicyDeploymentState State, DateTimeOffset? At)>();
        for (var page = 0; page < MaxPolicyPages; page++)
        {
            var table = await client.PostReportAsync(GraphRequest.PostJson(
                "deviceManagement/reports/getConfigurationPoliciesReportForDevice",
                ReportBody(filter, null, page * PolicyPageSize, PolicyPageSize, select), GraphVersion.Beta), cancellationToken).ConfigureAwait(false);
            for (var row = 0; row < table.Count; row++)
            {
                var type = table.Text(row, "UnifiedPolicyType_loc");
                rows.Add((table.Text(row, "PolicyId"), table.Text(row, "PolicyName"), type.Length > 0 ? type : table.Text(row, "UnifiedPolicyType"),
                    PolicyDeploymentStates.FromReport(table.Int(row, "PolicyStatus")), table.Date(row, "PspdpuLastModifiedTimeUtc")));
            }
            if (table.Count < PolicyPageSize || (page + 1) * PolicyPageSize >= table.TotalRowCount) break;
        }

        // A row per context (device, each user): keep the worst result and the latest report.
        return rows
            .GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var worst = group.MinBy(row => row.State);
                return new PolicyDeploymentStatus(group.Key, worst.Name, worst.Type, worst.State, group.Max(row => row.At));
            })
            .OrderBy(policy => policy.State)
            .ThenBy(policy => policy.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    internal static byte[] ReportBody(string filter, string? search, int skip, int top, IReadOnlyList<string>? select = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            if (select is not null)
            {
                writer.WriteStartArray("select");
                foreach (var column in select) writer.WriteStringValue(column);
                writer.WriteEndArray();
            }
            writer.WriteString("filter", filter);
            if (search is { Length: > 0 }) writer.WriteString("search", search);
            writer.WriteNumber("skip", skip);
            writer.WriteNumber("top", top);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static int HexToInt(string hex) =>
        hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        && uint.TryParse(hex.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? unchecked((int)value)
            : 0;
}
