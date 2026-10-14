using FieldKit.Core.Demo;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>Ports the view-model half of <c>InstallAppModelTests</c> (the members test is in AssignmentViewModelTests).</summary>
public sealed class InstallAppViewModelTests
{
    private static readonly IntuneDevice Device = new()
    {
        Id = "i-1", DeviceName = "PC1", SerialNumber = "S12345", UserPrincipalName = "alex.doe@contoso.com", UserDisplayName = "Alex Doe",
        ComplianceState = ComplianceState.Compliant, OperatingSystem = "Windows", AzureADDeviceId = "d-1", IsEncrypted = true,
    };

    private static readonly DirectoryGroup Group = new() { Id = "g-7zip", DisplayName = "APP-7-Zip", SecurityEnabled = true };

    private static readonly AssignedItem App = new() { Id = "a-1", Kind = AssignmentKind.App, Name = "Google Chrome", TypeName = "win32LobApp" };

    private static InstallAppViewModel Model(IGraphClient client, string? entraObjectId = "obj-1", IntuneDevice? device = null) =>
        new(device ?? Device, entraObjectId, new AssignmentCatalog(client), new DirectoryLookup(client),
            new GroupMembershipUpdates(client), new DeviceActions(client), () => { });

    private static string BodyText(GraphRequest request) => System.Text.Encoding.UTF8.GetString(request.Body ?? []);

    [Fact]
    public async Task AppsAreFilteredToTheDevicePlatform()
    {
        var stub = new StubGraphClient(new() { ["deviceAppManagement/mobileApps"] = "mobile_apps_assignments" });
        var model = Model(stub);
        await model.LoadAsync();
        Assert.Equal(SectionKind.Loaded, model.Apps.Kind);
        // The iOS app and the unknown type are left out for a Windows device.
        Assert.Equal(["Company Portal", "Google Chrome"], model.Apps.Value!.Select(app => app.Name));
        model.Query = "chrome";
        Assert.Equal(["Google Chrome"], model.AppRows.Select(row => row.Name));
    }

    [Fact]
    public async Task AddingTheDeviceUsesItsObjectIdAndMarksMembership()
    {
        var stub = new StubGraphClient(new() { ["groups/g-7zip/members/$ref"] = "" });
        var model = Model(stub);
        Assert.True(await model.AddAsync(InstallMember.Device, Group, App));
        Assert.True(model.IsMember(InstallMember.Device, Group));
        Assert.Contains("directoryObjects/obj-1", BodyText(stub.Sent[^1]), StringComparison.Ordinal);
        var added = Assert.IsType<InstallOutcome.Added>(model.Outcome);
        Assert.Contains("Google Chrome", added.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddingTheUserLooksUpTheirObjectIdFirst()
    {
        var stub = new StubGraphClient(new()
        {
            ["users/alex.doe@contoso.com"] = "single_user",
            ["groups/g-7zip/members/$ref"] = "",
        });
        var model = Model(stub);
        Assert.True(await model.AddAsync(InstallMember.User, Group, App));
        Assert.Contains("directoryObjects/u-1", BodyText(stub.Sent[^1]), StringComparison.Ordinal);
        Assert.True(model.IsMember(InstallMember.User, Group));
    }

    [Fact]
    public async Task AddThenRemoveTheDevice()
    {
        var stub = new StubGraphClient(new()
        {
            ["groups/g-7zip/members/$ref"] = "",
            ["groups/g-7zip/members/obj-1/$ref"] = "",
        });
        var model = Model(stub);
        Assert.True(await model.AddAsync(InstallMember.Device, Group, App));
        Assert.True(model.IsDirectMember(InstallMember.Device, Group));
        Assert.True(await model.RemoveAsync(InstallMember.Device, Group));
        Assert.False(model.IsDirectMember(InstallMember.Device, Group));
        Assert.False(model.IsMember(InstallMember.Device, Group));
        Assert.Contains(stub.Sent, request => request.Method == GraphMethod.Delete);
    }

    [Fact]
    public async Task RemovingANestedMemberExplains()
    {
        var stub = new StubGraphClient(new() { ["groups/g-7zip/members/obj-1/$ref"] = GraphException.NotFound() });
        var model = Model(stub);
        Assert.False(await model.RemoveAsync(InstallMember.Device, Group));
        var failed = Assert.IsType<InstallOutcome.Failed>(model.Outcome);
        Assert.Contains("nested group", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeviceWithoutEntraObjectCantJoin()
    {
        var stub = new StubGraphClient([]);
        var model = Model(stub, entraObjectId: null);
        Assert.False(await model.AddAsync(InstallMember.Device, Group, App));
        Assert.Empty(stub.Sent);
    }

    [Fact]
    public async Task ForbiddenNamesThePermissionAndRole()
    {
        var stub = new StubGraphClient(new() { ["groups/g-7zip/members/$ref"] = GraphException.Forbidden() });
        var model = Model(stub);
        Assert.False(await model.AddAsync(InstallMember.Device, Group, App));
        Assert.Equal(new InstallOutcome.Failed(GraphFeature.AppInstall.AccessDeniedMessage()), model.Outcome);
    }

    [Fact]
    public async Task OptionsListTheRequiredGroupsAndWhatElseTheyBringIn()
    {
        var demo = new DemoGraphClient(TimeSpan.Zero);
        var cts = new IntuneDevice
        {
            Id = "i-1", DeviceName = "CTS-LT-01234", UserPrincipalName = "alex.doe@contoso.com", UserDisplayName = "Alex Doe",
            OperatingSystem = "Windows", AzureADDeviceId = "c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b",
        };
        var model = Model(demo, entraObjectId: "e-1", device: cts);
        await model.LoadAsync();
        var sevenZip = model.Apps.Value!.Single(app => app.Name == "7-Zip");
        Assert.Equal(AppInstallPlan.StatusKind.NotRequired, model.StatusOf(sevenZip).Kind);

        await model.SelectAsync(sevenZip);
        Assert.Equal(SectionKind.Loaded, model.OptionRows.Kind);
        var row = Assert.Single(model.OptionRows.Value!);
        Assert.Equal("APP-7-Zip-Install", row.Group.DisplayName);
        Assert.True(row.ShowsAddDevice);
        Assert.True(row.CanAddDevice);
        Assert.Equal("Add Alex Doe", row.AddUserText);
        // The group also carries a compliance policy.
        Assert.Equal("Also brings in: Developer tools policy", row.AlsoTargetsText);

        Assert.True(await model.AddAsync(InstallMember.Device, row.Group, sevenZip));
        Assert.True(row.ShowsRemoveDevice);
        Assert.False(row.ShowsAddDevice);
        Assert.Equal(AppInstallPlan.StatusKind.Required, model.SelectedStatusKind);

        var portal = model.Apps.Value!.Single(app => app.Name == "Company Portal");
        await model.SelectAsync(portal);
        Assert.Equal(SectionKind.NotFound, model.OptionRows.Kind);
        Assert.StartsWith("Required for all devices", model.NoGroupText, StringComparison.Ordinal);
    }
}
