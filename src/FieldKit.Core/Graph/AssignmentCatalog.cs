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

    /// <summary>How many items' assignments are read at once when they have to be read one by one.</summary>
    public const int ParallelAssignmentReads = 4;

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
        IReadOnlyList<AssignedItem> items;
        try
        {
            var request = GraphRequest.Get(path, [("$select", select), ("$expand", "assignments")], version: version);
            var objects = await _client.GetAllAsync<AssignableObjectDto>(request, MaxPages, cancellationToken).ConfigureAwait(false);
            items = objects.Select(item => item.ToDomain(kind)).ToList();
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.Server && error.Status >= 500)
        {
            // Seen on a real tenant (2026-10-16): remediations with $expand=assignments answered a
            // server error every time, while every other kind loaded. Listing the items alone and
            // reading each one's assignments still works, and shows which item Graph can't serve.
            items = await OneByOneAsync(kind, path, select, version, cancellationToken).ConfigureAwait(false);
        }
        lock (_lock)
        {
            _cache[kind] = (items, _time.GetTimestamp());
        }
        return items;
    }

    /// <summary>
    /// The items without <c>$expand</c>, then <c>{item}/assignments</c> for each, a few at a time. An
    /// item whose assignments Graph still can't serve is kept with <see cref="AssignedItem.AssignmentsUnread"/>,
    /// so the rest show and the page can say what's missing. Access, throttling and sign-in errors
    /// still fail the whole kind.
    /// </summary>
    private async Task<IReadOnlyList<AssignedItem>> OneByOneAsync(
        AssignmentKind kind, string path, string select, GraphVersion version, CancellationToken cancellationToken)
    {
        var list = GraphRequest.Get(path, [("$select", select)], version: version);
        var objects = await _client.GetAllAsync<AssignableObjectDto>(list, MaxPages, cancellationToken).ConfigureAwait(false);
        using var gate = new SemaphoreSlim(ParallelAssignmentReads);
        var reads = objects.Select(async item =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // GraphRequest escapes each path segment.
                var request = GraphRequest.Get($"{path}/{item.Id}/assignments", version: version);
                var assignments = await _client.GetAllAsync<AssignmentDto>(request, MaxPages, cancellationToken).ConfigureAwait(false);
                return item.ToDomain(kind) with { Assignments = assignments.Select(assignment => assignment.ToDomain()).ToList() };
            }
            catch (GraphException error) when (error.Kind is GraphErrorKind.Server or GraphErrorKind.NotFound or GraphErrorKind.Decoding)
            {
                return item.ToDomain(kind) with { AssignmentsUnread = true };
            }
            finally
            {
                gate.Release();
            }
        });
        return await Task.WhenAll(reads).ConfigureAwait(false);
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
