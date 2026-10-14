using FieldKit.Core.Demo;

namespace FieldKit.Core.Tests;

/// <summary>
/// New for Windows: every demo response decodes through the real DTOs and gives the results
/// the demo serials promise, so demo mode (and Store certification) can't silently break.
/// </summary>
public sealed class DemoGraphClientTests
{
    private const string HealthyEntraId = "c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b";

    private readonly DemoGraphClient _demo = new(TimeSpan.Zero);
    private static readonly AuditSettings Settings = new();

    [Fact]
    public async Task HealthySerialHasNoFlags()
    {
        var snapshot = await new DeviceLookup(_demo).SnapshotAsync("5CG1234XYZ", Settings);
        Assert.Empty(snapshot.Flags);
        Assert.Equal("CTS-LT-01234", snapshot.IntuneDeviceName);
        Assert.Equal("Hybrid joined", snapshot.TrustType);
        Assert.Equal("CTS-Standard", snapshot.AutopilotGroupTag);
    }

    [Fact]
    public async Task DuplicateSerialShowsEveryProblem()
    {
        var snapshot = await new DeviceLookup(_demo).SnapshotAsync("PF2ABCDE", Settings);
        Assert.Equal(
            new HashSet<FlagKind>
            {
                FlagKind.EntraDisabled, FlagKind.Stale, FlagKind.Noncompliant,
                FlagKind.NameMismatch, FlagKind.DuplicateIntuneRecords, FlagKind.NoPrimaryUser,
            },
            snapshot.Flags.Select(flag => flag.Kind).ToHashSet());
        Assert.Equal(FlagSeverity.Critical, snapshot.WorstSeverity);
    }

    [Theory]
    [InlineData("PF9NEW01", FlagKind.NotInIntune)]
    [InlineData("NOTREAL99", FlagKind.UnknownDevice)]
    public async Task OtherSerialsFlagAsDocumented(string serial, FlagKind kind)
    {
        var snapshot = await new DeviceLookup(_demo).SnapshotAsync(serial, Settings);
        Assert.Equal([kind], snapshot.Flags.Select(flag => flag.Kind));
    }

    [Fact]
    public async Task DeniedSerialIsForbidden()
    {
        var error = await Assert.ThrowsAsync<GraphException>(() => new DeviceLookup(_demo).SnapshotAsync("DENIED01", Settings));
        Assert.Equal(GraphErrorKind.Forbidden, error.Kind);
    }

    [Fact]
    public async Task BoxSerialsAreUntaggedAutopilotDevices()
    {
        var record = Assert.Single(await new DeviceLookup(_demo).AutopilotDevicesAsync("BOX00042"));
        Assert.Equal("", record.GroupTag);
        Assert.Equal(AutopilotEnrollmentState.NotContacted, record.EnrollmentState);
    }

    [Fact]
    public async Task DeviceFromAGroupFindsItsIntuneRecord()
    {
        // The iOS demo can't do this: its Entra-ID case sits behind the serial case and never runs.
        var device = Assert.Single(await new DeviceLookup(_demo).IntuneDevicesForEntraDeviceAsync(HealthyEntraId));
        Assert.Equal("i-1", device.Id);
    }

    [Fact]
    public async Task NameSearchMatchesPrefixes()
    {
        var devices = await new DeviceLookup(_demo).SearchDevicesAsync("cts-dt");
        Assert.Equal(["CTS-DT-00042"], devices.Select(device => device.DeviceName));
        var all = await new DeviceLookup(_demo).SearchDevicesAsync("cts");
        Assert.Equal(8, all.Count);
        Assert.All(all, device => Assert.StartsWith("CTS", device.DeviceName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SecretsDecode()
    {
        var secrets = new RecoverySecrets(_demo);
        var credential = await secrets.LocalAdminCredentialAsync(HealthyEntraId);
        Assert.Equal("Kp7#vQ2m-Lx9!dRw", credential?.Password);
        Assert.NotNull(credential?.RotatesAt);

        var keys = await secrets.BitLockerKeysAsync(HealthyEntraId, "e-1");
        var key = Assert.Single(keys.Keys);
        Assert.Equal("OS drive", key.Volume);
    }

    [Fact]
    public async Task EveryCatalogKindDecodes()
    {
        var catalog = new AssignmentCatalog(_demo);
        foreach (var kind in Enum.GetValues<AssignmentKind>())
        {
            Assert.NotEmpty(await catalog.ItemsAsync(kind));
        }
        var apps = await catalog.ItemsAsync(AssignmentKind.App);
        Assert.Equal("Google Chrome", apps[1].Name);
        Assert.Equal([AssignmentTarget.Group("g-laptops"), AssignmentTarget.ExcludedGroup("g-kiosk")], apps[1].Assignments.Select(entry => entry.Target));
    }

    [Fact]
    public async Task PeopleGroupsAndMembershipsDecode()
    {
        var directory = new DirectoryLookup(_demo);
        Assert.Equal(["Alex Doe"], (await directory.SearchUsersAsync("alex")).Select(user => user.DisplayName));
        var group = Assert.Single(await directory.SearchGroupsAsync("cts"));
        Assert.True(group.IsDynamic);
        Assert.Equal(["CTS Laptops", "Kiosk Devices"], (await directory.GroupsOfAsync("devices/e-1")).Select(item => item.DisplayName));
        Assert.Equal(["Finance Users"], (await directory.GroupsOfAsync("users/u-alex")).Select(item => item.DisplayName));
        Assert.Equal(3, (await directory.MembersAsync("g-laptops")).Count);
        Assert.Equal("u-alex", (await directory.UserAsync("alex.doe@contoso.com")).Id);
        Assert.Equal("APP-7-Zip-Install", (await directory.GroupAsync("g-7zip")).DisplayName);
        Assert.Equal(["CTS-LT-01234", "Alex's iPhone"], (await directory.ManagedDevicesAsync("u-alex")).Select(device => device.DeviceName));
    }

    [Fact]
    public async Task InventoryAndComplianceDecode()
    {
        var inventory = new DeviceInventoryLookup(_demo);
        Assert.NotNull((await inventory.InventoryAsync("i-1")).Storage);
        Assert.Equal("HP EliteBook 840 G10 Notebook PC", (await inventory.HardwareAsync("i-1")).ProductName);
        Assert.False((await inventory.DefenderAsync("i-1"))?.SignaturesAreStale());
        Assert.Equal(6, (await inventory.DetectedAppsAsync("i-1")).Count);

        var policies = await new DeviceLookup(_demo).CompliancePoliciesAsync("i-2");
        Assert.Equal(["Windows baseline", "Defender for Endpoint risk"], CompliancePolicyState.SortedForDisplay(policies).Select(policy => policy.DisplayName));
    }

    [Fact]
    public async Task WritesSucceedWithoutTheNetwork()
    {
        await new DeviceActions(_demo).PerformAsync(DeviceAction.Sync, "i-1");
        await new DeviceActions(_demo).RunRemediationAsync("r-1", "i-1");
        await new AutopilotUpdates(_demo).SetGroupTagAsync("CTS-Kiosk", "ap-BOX00042");
        await new GroupMembershipUpdates(_demo).AddMemberAsync("e-1", "g-7zip");
        await new GroupMembershipUpdates(_demo).RemoveMemberAsync("e-1", "g-7zip");
    }

    [Fact]
    public async Task OrganizationName()
    {
        Assert.Equal("Contoso (demo)", await _demo.OrganizationNameAsync());
    }

    [Fact]
    public async Task DemoSignInGatesTokens()
    {
        var auth = new DemoAuthProvider();
        Assert.Null(await auth.RestoreSessionAsync());
        var signedOut = await Assert.ThrowsAsync<AuthException>(() => auth.GetAccessTokenAsync(forceRefresh: false));
        Assert.Equal(AuthErrorKind.NoAccount, signedOut.Kind);

        var user = await auth.SignInAsync();
        Assert.Equal("demo@contoso.com", user.Username);
        Assert.Equal("demo-token", await auth.GetAccessTokenAsync(forceRefresh: false));
        Assert.Equal(GraphPermission.All.Count, GraphPermission.Granted(await auth.GrantedScopesAsync(forceRefresh: false))?.Count);

        await auth.SignOutAsync();
        Assert.Null(await auth.RestoreSessionAsync());
    }
}
