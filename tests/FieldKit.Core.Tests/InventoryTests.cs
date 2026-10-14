using System.Text;
using FieldKit.Core.Tests.Support;

namespace FieldKit.Core.Tests;

/// <summary>Ports InventoryTests.swift. Its <c>InventoryModelTests</c> move to M6 with the inventory view model.</summary>
public sealed class InventoryTests
{
    [Fact]
    public void StorageNeedsATotal()
    {
        Assert.Null(StorageUsage.Create(totalBytes: null, freeBytes: 10));
        Assert.Null(StorageUsage.Create(totalBytes: 0, freeBytes: 0));
        Assert.Null(StorageUsage.Create(totalBytes: 100, freeBytes: null));
        Assert.Equal(100, StorageUsage.Create(totalBytes: 100, freeBytes: 150)?.FreeBytes);
    }

    [Theory]
    [InlineData(50, StorageLevel.Ok)]
    [InlineData(15, StorageLevel.Ok)]
    [InlineData(14, StorageLevel.Low)]
    [InlineData(5, StorageLevel.Low)]
    [InlineData(4, StorageLevel.Critical)]
    [InlineData(0, StorageLevel.Critical)]
    public void StorageLevels(long freePercent, StorageLevel level)
    {
        Assert.Equal(level, StorageUsage.Create(totalBytes: 100, freeBytes: freePercent)?.Level);
    }

    [Fact]
    public void StorageSummaryUsesFileSizes()
    {
        var usage = Assert.IsType<StorageUsage>(StorageUsage.Create(totalBytes: 255_013_683_200, freeBytes: 120_259_084_288));
        Assert.Equal($"{StorageUsage.Format(120_259_084_288)} free of {StorageUsage.Format(255_013_683_200)}", usage.Summary);
        Assert.True(Math.Abs(usage.FreeShare - 0.4716) < 0.001);
    }

    [Fact]
    public void MacAddressesAreColonSeparated()
    {
        Assert.Equal("A1:B2:C3:D4:E5:F6", DeviceInventoryDto.Mac("a1b2c3d4e5f6"));
        Assert.Equal("A1:B2:C3:D4:E5:F6", DeviceInventoryDto.Mac("A1-B2-C3-D4-E5-F6"));
        Assert.Null(DeviceInventoryDto.Mac(""));
        Assert.Equal("weird", DeviceInventoryDto.Mac("weird"));
    }

    [Fact]
    public void ReadableIntuneLabels()
    {
        Assert.Equal("Windows Azure AD Join", IntuneLabels.Words("windowsAzureADJoin"));
        Assert.Equal("Device Enrollment Manager", IntuneLabels.Words("deviceEnrollmentManager"));
        Assert.Equal("Clean", IntuneLabels.Words("clean"));
        Assert.Equal("Android Enterprise Fully Managed", IntuneLabels.Words("androidEnterpriseFullyManaged"));
        Assert.Equal("Intune MDM", IntuneLabels.ManagementAgent("mdm"));
        Assert.Equal("Co-managed (Configuration Manager + Intune)", IntuneLabels.ManagementAgent("configurationManagerClientMdm"));
        Assert.Equal("Entra join", IntuneLabels.EnrollmentType("windowsAzureADJoin"));
        Assert.Equal("Some New Type", IntuneLabels.EnrollmentType("someNewType"));
    }

    [Fact]
    public async Task InventoryDecodesAndDropsBlanks()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1"] = "device_inventory" });
        var inventory = await new DeviceInventoryLookup(stub).InventoryAsync("i-1");
        Assert.NotNull(inventory.Storage);
        Assert.Equal("A1:B2:C3:D4:E5:F6", inventory.WifiMac);
        Assert.Null(inventory.EthernetMac);
        Assert.Null(inventory.Imei);
        Assert.Null(inventory.PhoneNumber);
        Assert.False(inventory.IsSupervised);
        var url = stub.Requests[0].AbsoluteUri;
        Assert.StartsWith("https://graph.microsoft.com/v1.0/", url, StringComparison.Ordinal);
        Assert.Contains("freeStorageSpaceInBytes", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HardwareComesFromBetaAndTreatsZerosAsUnreported()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1"] = "device_hardware" });
        var hardware = await new DeviceInventoryLookup(stub).HardwareAsync("i-1");
        Assert.StartsWith("https://graph.microsoft.com/beta/", stub.Requests[0].AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(17_179_869_184, hardware.PhysicalMemoryBytes);
        Assert.Null(hardware.ProcessorArchitecture);           // "unknown"
        Assert.Equal("2.0", hardware.TpmVersion);              // empty spec version falls back
        Assert.Equal(87, hardware.BatteryHealthPercentage);
        Assert.Null(hardware.BatteryChargeCycles);             // 0 means not reported
        Assert.Null(hardware.BatteryLevelPercentage);
        Assert.True(hardware.HasBattery);
        Assert.Equal(["10.0.0.5"], hardware.WiredIpAddresses);
        Assert.Equal("Enterprise", hardware.OperatingSystemEdition);
    }

    [Fact]
    public async Task DefenderDecodes()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1/windowsProtectionState"] = "windows_protection_state" });
        var defender = Assert.IsType<DefenderStatus>(await new DeviceInventoryLookup(stub).DefenderAsync("i-1"));
        Assert.True(defender.RealTimeProtectionEnabled);
        Assert.False(defender.TamperProtectionEnabled);
        Assert.True(defender.RebootRequired);
        Assert.Null(defender.LastFullScanAt);
        Assert.Equal("clean", defender.DeviceState);
        var updated = Assert.NotNull(defender.SignatureUpdatedAt);
        Assert.False(defender.SignaturesAreStale(updated + TimeSpan.FromDays(1)));
        Assert.True(defender.SignaturesAreStale(updated + TimeSpan.FromDays(4)));
    }

    [Fact]
    public async Task DefenderNotReportedIsNull()
    {
        var stub = new StubGraphClient(new());
        Assert.Null(await new DeviceInventoryLookup(stub).DefenderAsync("x"));
    }

    [Fact]
    public async Task DetectedAppsSortedFromBeta()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1/detectedApps"] = "detected_apps" });
        var apps = await new DeviceInventoryLookup(stub).DetectedAppsAsync("i-1");
        Assert.Equal(["7-Zip", "Unnamed app", "zoom Workplace"], apps.Select(app => app.Name));
        Assert.Equal(5_242_880, apps[0].SizeBytes);
        Assert.Null(apps[2].SizeBytes);
        Assert.StartsWith("https://graph.microsoft.com/beta/", stub.Requests[0].AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagedDeviceCarriesStorage()
    {
        var json = """{"id":"i-9","totalStorageSpaceInBytes":1000,"freeStorageSpaceInBytes":40}""";
        var device = GraphJson.Decode<ManagedDeviceDto>(Encoding.UTF8.GetBytes(json)).ToDomain();
        Assert.Equal(StorageLevel.Critical, device.Storage?.Level);
        Assert.Contains("freeStorageSpaceInBytes", ManagedDeviceDto.SelectFields, StringComparison.Ordinal);
    }
}
