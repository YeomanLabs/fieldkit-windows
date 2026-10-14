using FieldKit.Core.Tests.Support;

namespace FieldKit.Core.Tests;

public sealed class DeviceLookupTests
{
    private static readonly AuditSettings Settings = new() { StaleThresholdDays = 3650 };

    [Fact]
    public async Task AutopilotKeepsOnlyExactSerialMatches()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/windowsAutopilotDeviceIdentities"] = "autopilot_identities" });
        var records = await new DeviceLookup(stub).AutopilotDevicesAsync("5CG1234XYZ");
        Assert.Equal(["ap-0001"], records.Select(record => record.Id));
        Assert.Contains("contains(serialNumber,'5CG1234XYZ')", stub.Requests[0].AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilterEscapesQuotes()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices"] = "managedDevices_empty" });
        await new DeviceLookup(stub).IntuneDevicesAsync("A'B");
        Assert.Contains("serialNumber%20eq%20'A''B'", stub.Requests[0].AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotOfHealthyDevice()
    {
        var stub = new StubGraphClient(new()
        {
            ["deviceManagement/managedDevices"] = "managedDevices_single",
            ["devices"] = "devices_entra",
            ["deviceManagement/windowsAutopilotDeviceIdentities"] = "autopilot_identities",
        });
        var snapshot = await new DeviceLookup(stub).SnapshotAsync("5CG1234XYZ", Settings);
        Assert.Empty(snapshot.Flags);
        Assert.Equal("CTS-LT-01234", snapshot.IntuneDeviceName);
        Assert.Equal("CTS-LT-01234", snapshot.EntraDisplayName);
        Assert.Equal("Hybrid joined", snapshot.TrustType);
        Assert.Equal("Compliant", snapshot.Compliance);
        Assert.Equal("CTS-Standard", snapshot.AutopilotGroupTag);
        Assert.Equal("alex.doe@contoso.com", snapshot.PrimaryUser);
    }

    [Fact]
    public async Task SnapshotOfAutopilotOnlyDevice()
    {
        var stub = new StubGraphClient(new()
        {
            ["deviceManagement/managedDevices"] = "managedDevices_empty",
            ["deviceManagement/windowsAutopilotDeviceIdentities"] = "autopilot_only",
        });
        var snapshot = await new DeviceLookup(stub).SnapshotAsync("PF9NEW01", Settings);
        Assert.Equal([FlagKind.NotInIntune], snapshot.Flags.Select(flag => flag.Kind));
        Assert.Equal("CTS-Standard", snapshot.AutopilotGroupTag);
        Assert.Equal(2, stub.Sent.Count); // no Entra call without an Intune record
    }

    [Fact]
    public async Task SnapshotOfGarbageSerial()
    {
        var stub = new StubGraphClient(new()
        {
            ["deviceManagement/managedDevices"] = "managedDevices_empty",
            ["deviceManagement/windowsAutopilotDeviceIdentities"] = "autopilot_empty",
        });
        var snapshot = await new DeviceLookup(stub).SnapshotAsync("NOTREAL99", Settings);
        Assert.Equal([FlagKind.UnknownDevice], snapshot.Flags.Select(flag => flag.Kind));
        Assert.Equal(FlagSeverity.Warning, snapshot.WorstSeverity);
    }

    [Fact]
    public async Task SnapshotFlagsMissingEntraObject()
    {
        var stub = new StubGraphClient(new()
        {
            ["deviceManagement/managedDevices"] = "managedDevices_single",
            ["devices"] = "devices_empty",
            ["deviceManagement/windowsAutopilotDeviceIdentities"] = "autopilot_identities",
        });
        var snapshot = await new DeviceLookup(stub).SnapshotAsync("5CG1234XYZ", Settings);
        Assert.Equal([FlagKind.NoEntraObject], snapshot.Flags.Select(flag => flag.Kind));
        Assert.Equal(FlagSeverity.Critical, snapshot.WorstSeverity);
    }

    [Fact]
    public async Task AutopilotFailureSuppressesDependentFlagsOnly()
    {
        var stub = new StubGraphClient(new()
        {
            ["deviceManagement/managedDevices"] = "managedDevices_empty",
            ["deviceManagement/windowsAutopilotDeviceIdentities"] = GraphException.Forbidden(),
        });
        var snapshot = await new DeviceLookup(stub).SnapshotAsync("5CG1234XYZ", Settings);
        Assert.Empty(snapshot.Flags);
    }

    [Fact]
    public async Task IntuneForbiddenFailsSnapshot()
    {
        var stub = new StubGraphClient(new()
        {
            ["deviceManagement/managedDevices"] = GraphException.Forbidden(),
            ["deviceManagement/windowsAutopilotDeviceIdentities"] = "autopilot_empty",
        });
        var error = await Assert.ThrowsAsync<GraphException>(() => new DeviceLookup(stub).SnapshotAsync("5CG1234XYZ", Settings));
        Assert.Equal(GraphErrorKind.Forbidden, error.Kind);
    }
}
