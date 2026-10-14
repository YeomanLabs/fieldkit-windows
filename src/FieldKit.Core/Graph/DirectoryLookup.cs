using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>People and group search, memberships, and a person's devices.</summary>
public sealed class DirectoryLookup(IGraphClient client)
{
    /// <summary>Rows per page in the Devices, Users and Groups lists.</summary>
    public const int PageSize = 100;

    private static readonly IReadOnlyDictionary<string, string> EventualConsistency =
        new Dictionary<string, string> { ["ConsistencyLevel"] = "eventual" };

    /// <summary>
    /// Matches the start of words in display name, UPN or mail. Uses Graph's advanced
    /// query <c>$search</c>, which needs the ConsistencyLevel header.
    /// </summary>
    public async Task<IReadOnlyList<DirectoryUser>> SearchUsersAsync(string text, int top = 15, CancellationToken cancellationToken = default)
    {
        if (ODataLiteral.SearchTerm(text) is not { } term) return [];
        var search = string.Join(" OR ", new[] { "displayName", "userPrincipalName", "mail" }.Select(field => $"\"{field}:{term}\""));
        var request = GraphRequest.Get("users",
        [
            ("$search", search),
            ("$select", UserDto.SelectFields),
            ("$top", Count(top)),
        ], EventualConsistency);
        var items = await client.GetAllAsync<UserDto>(request, maxPages: 1, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToDomain()).ToList();
    }

    /// <summary>Groups whose name starts with the search words.</summary>
    public async Task<IReadOnlyList<DirectoryGroup>> SearchGroupsAsync(string text, int top = 15, CancellationToken cancellationToken = default)
    {
        if (ODataLiteral.SearchTerm(text) is not { } term) return [];
        var request = GraphRequest.Get("groups",
        [
            ("$search", $"\"displayName:{term}\""),
            ("$select", DirectoryObjectDto.GroupSelect),
            ("$top", Count(top)),
        ], EventualConsistency);
        var items = await client.GetAllAsync<DirectoryObjectDto>(request, maxPages: 1, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToGroup()).ToList();
    }

    /// <summary>Every user, A–Z by display name, a page at a time (the Users tab).</summary>
    public Task<GraphPage<DirectoryUser>> UsersPageAsync(Uri? next = null, CancellationToken cancellationToken = default)
    {
        var first = GraphRequest.Get("users",
        [
            ("$select", UserDto.SelectFields),
            ("$orderby", "displayName"),
            ("$top", Count(PageSize)),
        ]);
        var request = next is null ? first : GraphClientExtensions.NextPage(next, first);
        return client.GetPageAsync<UserDto, DirectoryUser>(request, dto => dto.ToDomain(), cancellationToken);
    }

    /// <summary>Every group, A–Z by name, a page at a time (the Groups tab).</summary>
    public Task<GraphPage<DirectoryGroup>> GroupsPageAsync(Uri? next = null, CancellationToken cancellationToken = default)
    {
        var first = GraphRequest.Get("groups",
        [
            ("$select", DirectoryObjectDto.GroupSelect),
            ("$orderby", "displayName"),
            ("$top", Count(PageSize)),
        ]);
        var request = next is null ? first : GraphClientExtensions.NextPage(next, first);
        return client.GetPageAsync<DirectoryObjectDto, DirectoryGroup>(request, dto => dto.ToGroup(), cancellationToken);
    }

    private static string Count(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Groups a user or Entra device is in, by default including through nested groups.
    /// <paramref name="objectPath"/> is "users/{id or UPN}" or "devices/{object ID}". Direct
    /// membership (<paramref name="transitive"/> false) is what can be removed.
    /// </summary>
    public async Task<IReadOnlyList<DirectoryGroup>> GroupsOfAsync(string objectPath, bool transitive = true, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"{objectPath}/{(transitive ? "transitiveMemberOf" : "memberOf")}",
            [("$select", DirectoryObjectDto.GroupSelect)]);
        var items = await client.GetAllAsync<DirectoryObjectDto>(request, maxPages: 5, cancellationToken).ConfigureAwait(false);
        return items
            .Where(item => item.IsGroup)
            .Select(item => item.ToGroup())
            .OrderBy(group => group.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>One user by ID or UPN, for the object ID a group membership needs.</summary>
    public async Task<DirectoryUser> UserAsync(string idOrUpn, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"users/{idOrUpn}", [("$select", UserDto.SelectFields)]);
        return (await client.GetAsync<UserDto>(request, cancellationToken).ConfigureAwait(false)).ToDomain();
    }

    /// <summary>One group by ID, to tell whether members can be added (dynamic groups can't).</summary>
    public async Task<DirectoryGroup> GroupAsync(string id, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"groups/{id}", [("$select", DirectoryObjectDto.GroupSelect)]);
        return (await client.GetAsync<DirectoryObjectDto>(request, cancellationToken).ConfigureAwait(false)).ToGroup();
    }

    /// <summary>Direct members of a group: people, devices and nested groups (first 200).</summary>
    public async Task<IReadOnlyList<GroupMember>> MembersAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"groups/{groupId}/members", [("$top", "100")]);
        var items = await client.GetAllAsync<DirectoryObjectDto>(request, maxPages: 2, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToMember()).ToList();
    }

    /// <summary>Intune devices where this person is the primary user.</summary>
    public async Task<IReadOnlyList<IntuneDevice>> ManagedDevicesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"users/{userId}/managedDevices", [("$select", ManagedDeviceDto.SelectFields)]);
        var items = await client.GetAllAsync<ManagedDeviceDto>(request, maxPages: 2, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToDomain()).ToList();
    }
}
