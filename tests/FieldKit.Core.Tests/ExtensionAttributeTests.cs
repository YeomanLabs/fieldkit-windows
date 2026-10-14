using System.Text;
using FieldKit.Core.Demo;
using FieldKit.Core.Persistence;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows (2026-10-13): reading and editing an Entra device's extensionAttribute1–15.</summary>
public sealed class ExtensionAttributeTests : IDisposable
{
    private readonly SqliteLocalStore _store = SqliteLocalStore.OpenInMemory();
    private int _expired;

    public void Dispose() => _store.Dispose();

    private static EntraDevice Device(params string?[] values) =>
        new() { Id = "e-9", DisplayName = "CTS-KIOSK-01", ExtensionAttributes = new DeviceExtensionAttributes(values) };

    private static StubGraphClient Accepting() => new([], request => request.Method == GraphMethod.Patch ? new StubReply("", null) : null);

    private ExtensionAttributesEditViewModel Edit(EntraDevice device, IGraphClient client) =>
        new(device, new EntraDeviceUpdates(client), () => _expired++);

    [Fact]
    public async Task EntraLookupReadsTheAttributes()
    {
        var stub = new StubGraphClient(new() { ["devices"] = "devices_extension_attributes" });
        var device = await new DeviceLookup(stub).EntraDeviceAsync("c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b");

        var attributes = Assert.IsType<EntraDevice>(device).ExtensionAttributes;
        Assert.Equal("Kiosk", attributes[1]);
        Assert.Equal("", attributes[2]);
        Assert.Equal("", attributes[3]);
        Assert.Equal([(1, "Kiosk"), (15, "Lobby, Building 2")], attributes.Set.Select(attribute => (attribute.Number, attribute.Value)));
        Assert.Equal("extensionAttribute15", attributes.Set[^1].Name);
        Assert.Contains("extensionAttributes", Uri.UnescapeDataString(stub.Requests[0].Query), StringComparison.Ordinal);
    }

    [Fact]
    public void ADeviceWithoutTheObjectHasNone()
    {
        var device = GraphJson.Decode<GraphCollection<EntraDeviceDto>>(Fixture.Data("devices_entra")).Value[0].ToDomain();
        Assert.True(device.ExtensionAttributes.IsEmpty);
        Assert.Equal(DeviceExtensionAttributes.None, device.ExtensionAttributes);
    }

    [Fact]
    public void AttributesCompareByValueAndApplyChanges()
    {
        var before = new DeviceExtensionAttributes(["Finance", null, "Laptop"]);
        Assert.Equal(new DeviceExtensionAttributes(["Finance", "", " Laptop "]), before);
        var after = before.With(new Dictionary<int, string?> { [1] = null, [2] = "Kiosk", [15] = "HQ" });
        Assert.Equal([(2, "Kiosk"), (3, "Laptop"), (15, "HQ")], after.Set.Select(attribute => (attribute.Number, attribute.Value)));
        Assert.Equal("Finance", before[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => before[16]);
        Assert.Throws<ArgumentOutOfRangeException>(() => DeviceExtensionAttributes.Name(0));
    }

    [Fact]
    public async Task OnlyChangedAttributesAreSentInOnePatch()
    {
        var stub = Accepting();
        await new EntraDeviceUpdates(stub).SetExtensionAttributesAsync("e-9", new Dictionary<int, string?> { [7] = null, [3] = " Kiosk " });

        var request = Assert.Single(stub.Sent);
        Assert.Equal(GraphMethod.Patch, request.Method);
        Assert.Equal(new Uri("https://graph.microsoft.com/v1.0/devices/e-9"), request.Url);
        Assert.Equal("""{"extensionAttributes":{"extensionAttribute3":"Kiosk","extensionAttribute7":null}}""", Encoding.UTF8.GetString(request.Body!));

        await new EntraDeviceUpdates(stub).SetExtensionAttributesAsync("e-9", new Dictionary<int, string?>());
        Assert.Single(stub.Sent);
    }

    [Fact]
    public async Task TheDialogTracksChangesAndSavesThem()
    {
        var stub = Accepting();
        var edit = Edit(Device("Finance", null, "Laptop"), stub);
        Assert.Equal(15, edit.Fields.Count);
        Assert.False(edit.CanSave);
        Assert.Equal("No changes", edit.ChangeSummary);

        edit.Fields[1].Text = "   ";
        Assert.False(edit.HasChanges);
        edit.Fields[1].Text = " Kiosk ";
        edit.Fields[0].Text = "";
        edit.Fields[2].Text = "Desktop";
        Assert.Equal(["Cleared", "New", "Changed"], edit.Fields.Take(3).Select(box => box.StateText));
        Assert.Equal("3 changes", edit.ChangeSummary);
        Assert.True(edit.CanSave);

        Assert.True(await edit.SaveAsync());
        Assert.Equal(
            """{"extensionAttributes":{"extensionAttribute1":null,"extensionAttribute2":"Kiosk","extensionAttribute3":"Desktop"}}""",
            Encoding.UTF8.GetString(Assert.Single(stub.Sent).Body!));
        Assert.Equal(new DeviceExtensionAttributes([null, "Kiosk", "Desktop"]), edit.Saved);
        Assert.False(edit.IsSaving);
    }

    [Fact]
    public async Task AccessDeniedNamesThePermissionAndRole()
    {
        var edit = Edit(Device(), new StubGraphClient([], _ => GraphException.Forbidden()));
        edit.Fields[4].Text = "Kiosk";
        Assert.False(await edit.SaveAsync());
        Assert.Equal(GraphFeature.ExtensionAttributes.AccessDeniedMessage(), edit.ErrorMessage);
        Assert.Contains("Directory.AccessAsUser.All", edit.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Intune Administrator", edit.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(edit.Saved);

        // Typing again clears the message.
        edit.Fields[4].Text = "Kiosk 2";
        Assert.Null(edit.ErrorMessage);
    }

    [Fact]
    public async Task TooLongIsCaughtBeforeSending()
    {
        var stub = Accepting();
        var edit = Edit(Device(), stub);
        edit.Fields[0].Text = new string('x', DeviceExtensionAttributes.MaxLength + 1);
        Assert.False(await edit.SaveAsync());
        Assert.Equal("extensionAttribute1 is longer than 1024 characters.", edit.ErrorMessage);
        Assert.Empty(stub.Sent);
    }

    [Fact]
    public async Task AnExpiredSessionGoesBackToSignIn()
    {
        var edit = Edit(Device(), new StubGraphClient([], _ => GraphException.Unauthorized()));
        edit.Fields[0].Text = "Kiosk";
        Assert.False(await edit.SaveAsync());
        Assert.Equal(1, _expired);
        Assert.Null(edit.ErrorMessage);
    }

    [Fact]
    public async Task TheDevicePageListsThemAndShowsWhatWasSaved()
    {
        var model = new DeviceDetailViewModel("5CG1234XYZ", null, new DeviceLookup(new DemoGraphClient(TimeSpan.Zero)), new SettingsStore(_store), _store, () => { });
        await model.LoadAsync();
        Assert.True(model.CanEditExtensionAttributes);
        Assert.Equal(["Finance", "Standard laptop", "Contoso HQ"], model.ExtensionAttributeList.Select(attribute => attribute.Value));
        Assert.False(model.HasEntraNotice);

        model.ExtensionAttributesSaved(model.EntraDevice!.ExtensionAttributes.With(new Dictionary<int, string?> { [1] = null, [4] = "Kiosk" }));
        Assert.Equal([3, 4, 10], model.ExtensionAttributeList.Select(attribute => attribute.Number));
        Assert.True(model.HasEntraNotice);

        await model.LoadEntraAsync();
        Assert.False(model.HasEntraNotice);
    }

    [Fact]
    public void ThePermissionIsOptional()
    {
        var permission = Assert.Single(GraphPermission.All, permission => permission.Name == "Directory.AccessAsUser.All");
        Assert.True(permission.IsOptional);
        Assert.Equal("Not granted (optional)", new PermissionRow(permission, IsGranted: false).Status);
        Assert.False(new PermissionRow(permission, IsGranted: false).IsMissing);
        Assert.True(new PermissionRow(GraphPermission.All[0], IsGranted: false).IsMissing);
        Assert.Single(GraphPermission.All, permission => permission.IsOptional);
    }
}
