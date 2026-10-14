using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// Every assignable Intune object of each kind, with assignments, cached for a few minutes
/// so moving between devices, people and groups doesn't refetch the tenant each time.
/// </summary>
public sealed class AssignmentCatalog
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(300);
    public const int MaxPages = 20;

    private readonly IGraphClient _client;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly Dictionary<AssignmentKind, (IReadOnlyList<AssignedItem> Items, long FetchedAt)> _cache = [];

    public AssignmentCatalog(IGraphClient client, TimeProvider? time = null)
    {
        _client = client;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<AssignedItem>> ItemsAsync(
        AssignmentKind kind,
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!refresh)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(kind, out var cached) && _time.GetElapsedTime(cached.FetchedAt) < Lifetime)
                {
                    return cached.Items;
                }
            }
        }
        var (path, select, version) = Endpoint(kind);
        var request = GraphRequest.Get(path, [("$select", select), ("$expand", "assignments")], version: version);
        var objects = await _client.GetAllAsync<AssignableObjectDto>(request, MaxPages, cancellationToken).ConfigureAwait(false);
        var items = objects.Select(item => item.ToDomain(kind)).ToList();
        lock (_lock)
        {
            _cache[kind] = (items, _time.GetTimestamp());
        }
        return items;
    }

    public static (string Path, string Select, GraphVersion Version) Endpoint(AssignmentKind kind) =>
        kind switch
        {
            AssignmentKind.App => ("deviceAppManagement/mobileApps", "id,displayName,publisher", GraphVersion.V1),
            AssignmentKind.ConfigurationProfile => ("deviceManagement/deviceConfigurations", "id,displayName", GraphVersion.V1),
            AssignmentKind.CompliancePolicy => ("deviceManagement/deviceCompliancePolicies", "id,displayName", GraphVersion.V1),
            AssignmentKind.SettingsCatalog => ("deviceManagement/configurationPolicies", "id,name", GraphVersion.Beta),
            AssignmentKind.Script => ("deviceManagement/deviceManagementScripts", "id,displayName", GraphVersion.Beta),
            AssignmentKind.Remediation => ("deviceManagement/deviceHealthScripts", "id,displayName,publisher", GraphVersion.Beta),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
