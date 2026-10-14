using FieldKit.Core.Demo;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>Groups and assignments, group pages and members (M2). iOS tests these models through the UI, except members.</summary>
public sealed class AssignmentViewModelTests
{
    private static readonly DemoGraphClient Demo = new(TimeSpan.Zero);
    private int _expired;

    private AssignmentsViewModel Assignments(AssignmentSubject subject, IGraphClient? client = null) =>
        new(subject, new DirectoryLookup(client ?? Demo), new AssignmentCatalog(client ?? Demo), () => _expired++);

    private static AssignmentKindSection Section(IEnumerable<AssignmentKindSection> sections, AssignmentKind kind) =>
        sections.Single(section => section.Kind == kind);

    [Fact]
    public async Task DeviceResolvesThroughItsGroupsAndItsPrimaryUsersGroups()
    {
        var model = Assignments(new AssignmentSubject.Device("CTS-LT-01234", "e-1", "alex.doe@contoso.com"));
        Assert.True(model.HasDeviceGroups);
        Assert.True(model.HasUserGroups);
        Assert.Equal("Primary user's groups (alex.doe@contoso.com)", model.UserGroups!.Header);

        await model.LoadAsync();

        Assert.Equal(["CTS Laptops", "Kiosk Devices"], model.DeviceGroups!.Groups.Select(group => group.DisplayName));
        Assert.Equal("Device groups (2)", model.DeviceGroups.Header);
        // Directory roles in memberOf aren't groups.
        Assert.Equal(["Finance Users"], model.UserGroups.Groups.Select(group => group.DisplayName));

        var apps = Section(model.Sections, AssignmentKind.App);
        Assert.Equal(SectionKind.Loaded, apps.State.Kind);
        var portal = apps.Items.Single(item => item.Item.Name == "Company Portal");
        Assert.Equal(["All devices"], portal.IncludedVia);
        var chrome = apps.Items.Single(item => item.Item.Name == "Google Chrome");
        Assert.True(chrome.IsExcluded);
        Assert.Equal(["Kiosk Devices"], chrome.ExcludedVia);
        Assert.Contains(apps.Items, item => item.Item.Name == "Adobe Acrobat Reader");
        Assert.DoesNotContain(apps.Items, item => item.Item.Name == "Unassigned tool");

        // Nothing targets the device's groups for these.
        Assert.Equal(SectionKind.Loaded, Section(model.Sections, AssignmentKind.Remediation).State.Kind);
        Assert.Equal(0, _expired);
    }

    [Fact]
    public async Task DeviceWithoutEntraObjectOrUserHasNoGroupSections()
    {
        var model = Assignments(new AssignmentSubject.Device("PF9NEW01", null, null));
        Assert.False(model.HasDeviceGroups);
        Assert.False(model.HasUserGroups);
        await model.LoadAsync();
        // Only "All devices" targets apply.
        var apps = Section(model.Sections, AssignmentKind.App);
        Assert.Equal(["Company Portal"], apps.Items.Select(item => item.Item.Name));
        Assert.Equal(SectionKind.NotFound, Section(model.Sections, AssignmentKind.Script).State.Kind);
    }

    [Fact]
    public async Task PersonGetsUserTargetsButNotAllDevices()
    {
        var alex = new DirectoryUser { Id = "u-alex", DisplayName = "Alex Doe", UserPrincipalName = "alex.doe@contoso.com" };
        var model = Assignments(new AssignmentSubject.Person(alex));
        Assert.False(model.HasDeviceGroups);
        Assert.Equal("Groups", model.UserGroups!.Header);
        await model.LoadAsync();
        var apps = Section(model.Sections, AssignmentKind.App).Items.Select(item => item.Item.Name).ToList();
        Assert.Contains("Outlook", apps);
        Assert.DoesNotContain("Company Portal", apps);
        Assert.StartsWith("What Intune aims at Alex Doe", model.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACatalogFailureNamesItsPermissionAndOtherKindsStillLoad()
    {
        var stub = new StubGraphClient(new()
        {
            ["devices/e-1/transitiveMemberOf"] = "group_members",
            ["deviceAppManagement/mobileApps"] = GraphException.Forbidden(),
        });
        var model = Assignments(new AssignmentSubject.Device("PC", "e-1", null), stub);
        await model.LoadAsync();
        var apps = Section(model.Sections, AssignmentKind.App);
        Assert.Equal(SectionKind.Failed, apps.State.Kind);
        Assert.Equal(GraphFeature.AppAssignments.AccessDeniedMessage(), apps.State.Message);
        Assert.Equal(GraphFeature.PolicyAssignments.Message(GraphException.NotFound()), Section(model.Sections, AssignmentKind.ConfigurationProfile).State.Message);
    }

    [Fact]
    public async Task UnauthorizedEndsTheSession()
    {
        var stub = new StubGraphClient([], _ => new StubReply(null, GraphException.Unauthorized()));
        var model = Assignments(new AssignmentSubject.Device("PC", "e-1", "a@b.c"), stub);
        await model.LoadAsync();
        Assert.True(_expired > 0);
        Assert.Equal(SectionKind.Loading, model.DeviceGroups!.State.Kind);
    }

    [Fact]
    public async Task GroupPageShowsWhatTargetsTheGroup()
    {
        var group = new DirectoryGroup { Id = "g-laptops", DisplayName = "CTS Laptops", GroupTypes = ["DynamicMembership"], MembershipRule = "(device.displayName -startsWith \"CTS-LT\")", SecurityEnabled = true };
        var model = new GroupViewModel(group, new AssignmentCatalog(Demo), () => _expired++);
        Assert.True(model.HasMembershipRule);
        Assert.Equal("Security · Dynamic", model.KindTitle);
        await model.LoadAsync();
        var apps = Section(model.Sections, AssignmentKind.App).Items;
        Assert.Equal(["7-Zip", "Google Chrome"], apps.Select(item => item.Item.Name));
        Assert.Equal(SectionKind.NotFound, Section(model.Sections, AssignmentKind.CompliancePolicy).State.Kind);
        Assert.Equal("Nothing targets this group.", Section(model.Sections, AssignmentKind.CompliancePolicy).NotFoundText);
    }

    [Fact]
    public async Task GroupMembersRemoveUpdatesTheList()
    {
        var stub = new StubGraphClient(new()
        {
            ["groups/g-1/members"] = "group_members",
            ["groups/g-1/members/u-1/$ref"] = "",
            ["groups/g-1/members/e-1/$ref"] = GraphException.Forbidden(),
        });
        var group = new DirectoryGroup { Id = "g-1", DisplayName = "Pilot", SecurityEnabled = true };
        var members = new GroupMembersViewModel(group, new DirectoryLookup(stub), new GroupMembershipUpdates(stub), () => { });
        await members.LoadAsync();
        Assert.Equal(SectionKind.Loaded, members.Members.Kind);
        var user = members.MemberList.Single(member => member.Id == "u-1");
        var device = members.MemberList.Single(member => member.Id == "e-1");

        Assert.True(await members.RemoveAsync(user));
        Assert.DoesNotContain(members.MemberList, member => member.Id == "u-1");
        Assert.False(members.NoticeIsError);
        Assert.Null(members.Removing);

        Assert.False(await members.RemoveAsync(device));
        Assert.Equal(GraphFeature.GroupMembership.AccessDeniedMessage(), members.Notice);
        Assert.True(members.NoticeIsError);
    }

    [Fact]
    public async Task DynamicGroupMembersCantBeRemoved()
    {
        var group = new DirectoryGroup { Id = "g-laptops", DisplayName = "CTS Laptops", GroupTypes = ["DynamicMembership"] };
        var members = new GroupMembersViewModel(group, new DirectoryLookup(Demo), new GroupMembershipUpdates(Demo), () => { });
        await members.LoadAsync();
        Assert.Equal(["Alex Doe", "CTS-LT-01234", "Kiosk Devices"], members.MemberList.Select(member => member.DisplayName));
        Assert.All(members.MemberList, member => Assert.False(members.CanRemove(member)));
        Assert.Contains("dynamic group", members.Footer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntraDeviceResolvesToItsIntuneRecord()
    {
        var model = new EntraDeviceViewModel("c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b", "CTS-LT-01234", new DeviceLookup(Demo), () => { });
        await model.ResolveAsync();
        Assert.Equal(SectionKind.Loaded, model.State.Kind);
        Assert.Equal("5CG1234XYZ", model.State.Value!.SerialNumber);

        var missing = new EntraDeviceViewModel("00000000-1111-2222-3333-444444444444", "OLD-PC", new DeviceLookup(Demo), () => { });
        await missing.ResolveAsync();
        Assert.Equal(SectionKind.NotFound, missing.State.Kind);
        Assert.Equal("Not in Intune", missing.Title);
        Assert.Equal("OLD-PC is in Entra but has no Intune record.", missing.Message);
    }
}
