using FieldKit.Core.Demo;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>Ports <c>InventoryModelTests</c>, plus the rows the tab shows.</summary>
public sealed class InventoryViewModelTests
{
    private static IntuneDevice Device(string os) => new()
    {
        Id = "i-1", DeviceName = "PC1", SerialNumber = "S12345", OperatingSystem = os,
        ComplianceState = ComplianceState.Compliant, IsEncrypted = true,
    };

    [Fact]
    public async Task SectionsLoadAndFailIndependently()
    {
        var stub = new StubGraphClient(new()
        {
            ["deviceManagement/managedDevices/i-1/windowsProtectionState"] = "windows_protection_state",
            ["deviceManagement/managedDevices/i-1/detectedApps"] = GraphException.Server(400, "BadRequest"),
        }, request =>
        {
            if (!request.Url.AbsolutePath.EndsWith("/i-1", StringComparison.Ordinal)) return null;
            return request.Url.AbsoluteUri.Contains("/beta/", StringComparison.Ordinal) ? GraphException.Forbidden() : "device_inventory";
        });
        var model = new DeviceInventoryViewModel(Device("Windows"), new DeviceInventoryLookup(stub), () => { });
        await model.LoadAsync();
        Assert.Equal(SectionKind.Loaded, model.Inventory.Kind);
        Assert.Equal(SectionKind.Failed, model.Hardware.Kind);
        Assert.Equal(SectionKind.Loaded, model.Defender?.Kind);
        Assert.Equal(SectionKind.Failed, model.Apps.Kind);
    }

    [Fact]
    public async Task NonWindowsSkipsDefender()
    {
        var model = new DeviceInventoryViewModel(Device("iOS"), new DeviceInventoryLookup(new StubGraphClient([])), () => { });
        Assert.Null(model.Defender);
        await model.LoadAsync();
        Assert.Null(model.Defender);
        Assert.Equal(SectionKind.Skipped, model.DefenderState.Kind);
    }

    [Fact]
    public async Task DemoDeviceShowsEverySection()
    {
        var device = Device("Windows") with { DeviceName = "CTS-LT-01234", Manufacturer = "HP", Model = "EliteBook 840 G10" };
        var model = new DeviceInventoryViewModel(device, new DeviceInventoryLookup(new DemoGraphClient(TimeSpan.Zero)), () => { });
        await model.LoadAsync();

        Assert.True(model.HasStorage);
        Assert.Equal(["Network", "Management"], model.InventorySections.Select(section => section.Title));
        Assert.Equal("Entra join", model.InventorySections[1].Rows.Single(row => row.Label == "Enrollment").Value);

        var hardware = model.HardwareRows.ToDictionary(row => row.Label, row => row.Value);
        Assert.Equal("HP EliteBook 840 G10", hardware["Model"]);
        Assert.Equal("16 GB", hardware["Memory"]);
        Assert.Equal("IFX 2.0", hardware["TPM"]);
        Assert.Equal("Windows Enterprise", hardware["OS"]);

        Assert.Equal(new InfoRow("Health", "87%"), model.BatteryRows[0]);
        Assert.Equal("Connectivity", Assert.Single(model.ConnectivitySections).Title);

        var defender = model.DefenderRows.ToDictionary(row => row.Label);
        Assert.Equal(InfoTone.Good, defender["Real-time protection"].Tone);
        Assert.Equal(InfoTone.Good, defender["Reboot required"].Tone);
        Assert.Equal("Never", defender["Last full scan"].Value);

        Assert.Equal("Apps on device (6)", model.AppsHeader);
        model.AppQuery = "pavlov";
        Assert.Equal(["7-Zip"], model.FilteredApps.Select(app => app.Name));
        Assert.Equal("Igor Pavlov · 5.00 MB", DeviceInventoryViewModel.AppDetail(model.FilteredApps[0]));
    }

    [Fact]
    public void MemoryUsesBinaryUnits()
    {
        Assert.Equal("16 GB", DeviceInventoryViewModel.MemoryText(17_179_869_184));
        Assert.Equal("512 MB", DeviceInventoryViewModel.MemoryText(536_870_912));
    }
}
