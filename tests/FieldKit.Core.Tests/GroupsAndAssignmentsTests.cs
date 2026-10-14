using FieldKit.Core.Tests.Support;

namespace FieldKit.Core.Tests;

/// <summary>
/// Ports GroupsAndAssignmentsTests.swift. Its <c>RemediationModelTests</c> move to M4 with the
/// device actions view model.
/// </summary>
public sealed class GroupsAndAssignmentsTests
{
    private static AssignedItem Item(string id, IReadOnlyList<AssignmentEntry> entries, AssignmentKind kind = AssignmentKind.App) =>
        new() { Id = id, Kind = kind, Name = id, Assignments = entries };

    private static AssignmentResolver.Membership Membership(IReadOnlyDictionary<string, string> groups, bool allDevices, bool allUsers) =>
        new(groups, allDevices, allUsers);

    // Resolver

    [Fact]
    public void DeviceGetsAllDevicesAndItsGroupsButNotOthers()
    {
        AssignedItem[] items =
        [
            Item("portal", [new AssignmentEntry(AssignmentTarget.AllDevices, "required")]),
            Item("chrome", [new AssignmentEntry(AssignmentTarget.Group("g-laptops"), "required")]),
            Item("finance", [new AssignmentEntry(AssignmentTarget.Group("g-finance"), "available")]),
            Item("teams", [new AssignmentEntry(AssignmentTarget.AllUsers, "available")]),
        ];
        var membership = Membership(new Dictionary<string, string> { ["g-laptops"] = "Laptops" }, allDevices: true, allUsers: false);
        var resolved = AssignmentResolver.Resolve(items, membership);
        Assert.Equal(["chrome", "portal"], resolved.Select(assignment => assignment.Item.Id));
        Assert.Equal(["Laptops"], resolved.Single(assignment => assignment.Id == "chrome").IncludedVia);
        Assert.Equal(["All devices"], resolved.Single(assignment => assignment.Id == "portal").IncludedVia);
        Assert.Equal(["Required"], resolved.Single(assignment => assignment.Id == "portal").Intents);
    }

    [Fact]
    public void ExclusionsAreShownLastAndMarked()
    {
        AssignedItem[] items =
        [
            Item("b-chrome",
            [
                new AssignmentEntry(AssignmentTarget.Group("g-laptops"), "required"),
                new AssignmentEntry(AssignmentTarget.ExcludedGroup("g-kiosk")),
            ]),
            Item("a-only-excluded", [new AssignmentEntry(AssignmentTarget.ExcludedGroup("g-kiosk"))]),
            Item("c-plain", [new AssignmentEntry(AssignmentTarget.Group("g-laptops"), "required")]),
        ];
        var membership = Membership(new Dictionary<string, string> { ["g-laptops"] = "Laptops", ["g-kiosk"] = "Kiosk" }, allDevices: false, allUsers: false);
        var resolved = AssignmentResolver.Resolve(items, membership);
        Assert.Equal(["c-plain", "a-only-excluded", "b-chrome"], resolved.Select(assignment => assignment.Item.Id));
        Assert.Equal(["Kiosk"], resolved[^1].ExcludedVia);
        Assert.Equal(["Laptops"], resolved[^1].IncludedVia);
        Assert.True(resolved[^1].IsExcluded);
    }

    [Fact]
    public void UserScopeAndDuplicateIntents()
    {
        AssignedItem[] items =
        [
            Item("vpn",
            [
                new AssignmentEntry(AssignmentTarget.AllUsers, "required"),
                new AssignmentEntry(AssignmentTarget.Group("g-1"), "required"),
                new AssignmentEntry(AssignmentTarget.Group("g-2"), "uninstall"),
            ]),
        ];
        var membership = Membership(new Dictionary<string, string> { ["g-1"] = "One", ["g-2"] = "Two" }, allDevices: false, allUsers: true);
        var resolved = Assert.Single(AssignmentResolver.Resolve(items, membership));
        Assert.Equal(["All users", "One", "Two"], resolved.IncludedVia);
        Assert.Equal(["Required", "Uninstall"], resolved.Intents);
    }

    [Fact]
    public void TargetingOneGroupIgnoresAllDevicesAndUsers()
    {
        AssignedItem[] items =
        [
            Item("portal", [new AssignmentEntry(AssignmentTarget.AllDevices, "required")]),
            Item("chrome", [new AssignmentEntry(AssignmentTarget.Group("g-1"), "required")]),
            Item("kiosk-block", [new AssignmentEntry(AssignmentTarget.ExcludedGroup("g-1"))]),
        ];
        var found = AssignmentResolver.Targeting("g-1", "One", items);
        Assert.Equal(["chrome", "kiosk-block"], found.Select(assignment => assignment.Item.Id));
        Assert.True(found[1].IsExcluded);
    }

    // Decoding and requests

    [Fact]
    public async Task AppAssignmentsDecodeEveryTargetShape()
    {
        var stub = new StubGraphClient(new() { ["deviceAppManagement/mobileApps"] = "mobile_apps_assignments" });
        var items = await new AssignmentCatalog(stub).ItemsAsync(AssignmentKind.App);
        Assert.Equal(5, items.Count);
        Assert.Equal("Windows app (Win32)", items[0].Detail);
        Assert.Equal([AssignmentTarget.Group("g-laptops"), AssignmentTarget.ExcludedGroup("g-kiosk")], items[0].Assignments.Select(entry => entry.Target));
        Assert.Equal(AssignmentTarget.AllDevices, items[1].Assignments[0].Target);
        Assert.Equal(AssignmentTarget.AllUsers, items[2].Assignments[0].Target);
        Assert.Equal("iOS store app", items[2].Detail);
        Assert.Equal("someFutureApp", items[3].Detail);
        Assert.Empty(items[4].Assignments);

        var url = stub.Requests[0].AbsoluteUri;
        Assert.StartsWith("https://graph.microsoft.com/v1.0/deviceAppManagement/mobileApps", url, StringComparison.Ordinal);
        Assert.Contains("$expand=assignments", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BetaOnlyKindsUseBetaAndSettingsCatalogUsesName()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/configurationPolicies"] = "settings_catalog_assignments" });
        var items = await new AssignmentCatalog(stub).ItemsAsync(AssignmentKind.SettingsCatalog);
        Assert.Equal("Edge security baseline", items[0].Name);
        Assert.StartsWith("https://graph.microsoft.com/beta/", stub.Requests[0].AbsoluteUri, StringComparison.Ordinal);
        foreach (var kind in new[] { AssignmentKind.Script, AssignmentKind.Remediation, AssignmentKind.SettingsCatalog })
        {
            Assert.Equal(GraphVersion.Beta, AssignmentCatalog.Endpoint(kind).Version);
        }
        foreach (var kind in new[] { AssignmentKind.App, AssignmentKind.ConfigurationProfile, AssignmentKind.CompliancePolicy })
        {
            Assert.Equal(GraphVersion.V1, AssignmentCatalog.Endpoint(kind).Version);
        }
    }

    [Fact]
    public async Task CatalogIsCachedUntilRefreshed()
    {
        var stub = new StubGraphClient(new() { ["deviceAppManagement/mobileApps"] = "mobile_apps_assignments" });
        var catalog = new AssignmentCatalog(stub);
        await catalog.ItemsAsync(AssignmentKind.App);
        await catalog.ItemsAsync(AssignmentKind.App);
        Assert.Single(stub.Sent);
        await catalog.ItemsAsync(AssignmentKind.App, refresh: true);
        Assert.Equal(2, stub.Sent.Count);
    }

    [Fact]
    public async Task GroupSearchUsesAdvancedQuery()
    {
        var stub = new StubGraphClient(new() { ["groups"] = "member_of" });
        await new DirectoryLookup(stub).SearchGroupsAsync("CTS \"laptops\"");
        var request = Assert.Single(stub.Sent);
        Assert.Equal("eventual", request.Headers["ConsistencyLevel"]);
        Assert.Equal("\"displayName:CTS laptops\"", RequestInspection.QueryValue(request.Url, "$search"));
    }

    [Fact]
    public async Task MembershipKeepsOnlyGroupsSorted()
    {
        var stub = new StubGraphClient(new() { ["devices/e-1/transitiveMemberOf"] = "member_of" });
        var groups = await new DirectoryLookup(stub).GroupsOfAsync("devices/e-1");
        Assert.Equal(["Alpha Laptops", "Zeta Devices"], groups.Select(group => group.DisplayName));
        Assert.True(groups[0].IsDynamic);
        Assert.Equal("Security · Dynamic", groups[0].KindTitle);
        Assert.Equal("(device.displayName -startsWith \"CTS\")", groups[0].MembershipRule);
        Assert.Equal("Security · Assigned", groups[1].KindTitle);
    }

    [Fact]
    public async Task GroupMembersByKind()
    {
        var stub = new StubGraphClient(new() { ["groups/g-1/members"] = "group_members" });
        var members = await new DirectoryLookup(stub).MembersAsync("g-1");
        Assert.Equal(
            [
                new GroupMemberKind.User("alex.doe@contoso.com"),
                new GroupMemberKind.Device("c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b", "Windows"),
                new GroupMemberKind.Group(),
                new GroupMemberKind.Other(),
            ],
            members.Select(member => member.Kind));
    }

    [Fact]
    public async Task DeviceByEntraIdFilters()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices"] = "managedDevices_single" });
        await new DeviceLookup(stub).IntuneDevicesForEntraDeviceAsync("abc");
        Assert.Contains("azureADDeviceId%20eq%20'abc'", stub.Requests[0].AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunRemediationPostsToBeta()
    {
        const string path = "deviceManagement/managedDevices/i-1/initiateOnDemandProactiveRemediation";
        var stub = new StubGraphClient(new() { [path] = "" });
        await new DeviceActions(stub).RunRemediationAsync("r-1", "i-1");
        var request = Assert.Single(stub.Sent);
        Assert.Equal(GraphMethod.Post, request.Method);
        Assert.Equal($"https://graph.microsoft.com/beta/{path}", request.Url.AbsoluteUri);
        Assert.Equal(new Dictionary<string, string> { ["scriptPolicyId"] = "r-1" }, RequestInspection.JsonBody(request.Body));
    }
}
