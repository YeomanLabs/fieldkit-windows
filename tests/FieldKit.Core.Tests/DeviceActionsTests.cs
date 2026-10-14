using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>Ports the actions, LAPS and BitLocker tests from <c>PhaseTwoThreeModelTests</c> and <c>RemediationModelTests</c>, plus the secrets dialog.</summary>
public sealed class DeviceActionsTests
{
    private const string EntraId = "c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b";

    private static readonly IntuneDevice Device = new()
    {
        Id = "i-1", DeviceName = "CTS-LT-01234", SerialNumber = "5CG1234XYZ", OperatingSystem = "Windows",
        ComplianceState = ComplianceState.Compliant, AzureADDeviceId = EntraId, IsEncrypted = true,
    };

    private static DeviceActionsViewModel Model(StubGraphClient stub, FakeOwnerVerifier verifier) =>
        new(new DeviceActions(stub), new RecoverySecrets(stub), verifier, () => { });

    [Fact]
    public async Task SyncSkipsHello()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1/syncDevice"] = "" });
        var verifier = new FakeOwnerVerifier();
        var model = Model(stub, verifier);
        Assert.True(await model.RunAsync(DeviceAction.Sync, Device));
        Assert.Empty(verifier.Reasons);
        Assert.Equal(new ActionStatus.Done(DeviceAction.Sync.SuccessMessage()), model.Status);
    }

    [Fact]
    public async Task FailedHelloSendsNothing()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1/retire"] = "" });
        var model = Model(stub, new FakeOwnerVerifier("Cancelled."));
        Assert.False(await model.RunAsync(DeviceAction.Retire, Device));
        Assert.Empty(stub.Sent);
        Assert.Equal(new ActionStatus.Failed("Cancelled."), model.Status);

        await model.ShowLocalAdminPasswordAsync("x", "CTS");
        await model.ShowBitLockerKeysAsync("x", null, "CTS");
        Assert.Empty(stub.Sent);
        Assert.Null(model.Revealed);
    }

    [Fact]
    public async Task RestartAfterHello()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1/rebootNow"] = "" });
        var verifier = new FakeOwnerVerifier();
        var model = Model(stub, verifier);
        Assert.True(await model.RunAsync(DeviceAction.Restart, Device));
        Assert.Equal(["Restart CTS-LT-01234"], verifier.Reasons);
        Assert.Single(stub.Sent);
    }

    [Fact]
    public async Task ForbiddenActionNamesThePermission()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1/rebootNow"] = GraphException.Forbidden() });
        var model = Model(stub, new FakeOwnerVerifier());
        Assert.False(await model.RunAsync(DeviceAction.Restart, Device));
        Assert.Equal(new ActionStatus.Failed(GraphFeature.DeviceActions.AccessDeniedMessage()), model.Status);
        Assert.True(model.StatusIsFailure);
    }

    [Fact]
    public async Task RevealsLapsAfterHello()
    {
        var stub = new StubGraphClient(new() { [$"directory/deviceLocalCredentials/{EntraId}"] = "device_local_credentials" });
        var verifier = new FakeOwnerVerifier();
        var model = Model(stub, verifier);
        await model.ShowLocalAdminPasswordAsync(EntraId, "CTS-LT-01234");
        var revealed = Assert.IsType<RevealedSecrets.LocalAdmin>(model.Revealed);
        Assert.Equal("Kp7#vQ2m-Lx9!dRw", revealed.Credential.Password);
        Assert.Equal(["Show the LAPS password for CTS-LT-01234"], verifier.Reasons);
        Assert.False(model.HasStatus);

        model.ForgetRevealed();
        Assert.Null(model.Revealed);
    }

    [Fact]
    public async Task BitLockerListedWithoutValuesExplainsRole()
    {
        var stub = new StubGraphClient(new()
        {
            ["informationProtection/bitlocker/recoveryKeys"] = "bitlocker_keys_list",
            ["informationProtection/bitlocker/recoveryKeys/b7f3a1c2-aaaa-4000-8000-000000000001"] = "autopilot_empty",
            ["informationProtection/bitlocker/recoveryKeys/b7f3a1c2-bbbb-4000-8000-000000000002"] = "autopilot_empty",
        });
        var model = Model(stub, new FakeOwnerVerifier());
        await model.ShowBitLockerKeysAsync("d1", null, "PC");
        var failed = Assert.IsType<ActionStatus.Failed>(model.Status);
        Assert.Contains("listed 2", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BitLockerNoneShowsTheDeviceIdUsed()
    {
        var stub = new StubGraphClient(new() { ["informationProtection/bitlocker/recoveryKeys"] = "autopilot_empty" });
        var model = Model(stub, new FakeOwnerVerifier());
        await model.ShowBitLockerKeysAsync("d1", "o1", "PC");
        var failed = Assert.IsType<ActionStatus.Failed>(model.Status);
        Assert.Contains("d1 or o1", failed.Text, StringComparison.Ordinal);
    }

    private static readonly GraphException EmptyBody = GraphException.Decoding($"{GraphException.EmptyResponseDetail}, HTTP 200, request-id r-1");

    /// <summary>Seen on a real tenant (2026-10-14): the LAPS API answered HTTP 200 with no body for a device with no password.</summary>
    [Fact]
    public async Task AnEmptyLapsResponseMeansNoPasswordAndPointsAHybridDeviceAtAd()
    {
        var stub = new StubGraphClient(new() { ["directory/deviceLocalCredentials/d1"] = EmptyBody });
        var model = Model(stub, new FakeOwnerVerifier());
        await model.ShowLocalAdminPasswordAsync("d1", "CTS-LT-40487", EntraTrustType.HybridJoined);
        var failed = Assert.IsType<ActionStatus.Failed>(model.Status);
        Assert.StartsWith("No LAPS password for CTS-LT-40487 is backed up to Microsoft Entra. It's hybrid joined", failed.Text, StringComparison.Ordinal);
        Assert.Contains("Get-LapsADPassword -Identity CTS-LT-40487", failed.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("couldn't read", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingLapsOnAnEntraJoinedDeviceChecksTheTenantSwitchAndPolicy()
    {
        var model = Model(new StubGraphClient([]), new FakeOwnerVerifier());
        await model.ShowLocalAdminPasswordAsync("d1", "PC", EntraTrustType.EntraJoined);
        var failed = Assert.IsType<ActionStatus.Failed>(model.Status);
        Assert.Contains("Devices → Device settings", failed.Text, StringComparison.Ordinal);
        Assert.Contains("Endpoint security → Account protection", failed.Text, StringComparison.Ordinal);

        await model.ShowLocalAdminPasswordAsync("d1", "PC", EntraTrustType.Registered);
        Assert.Contains("only registered in Entra", Assert.IsType<ActionStatus.Failed>(model.Status).Text, StringComparison.Ordinal);
    }

    /// <summary>Also seen there: a BitLocker lookup answered 404, which used to surface as a bare "Not found.".</summary>
    [Fact]
    public async Task BitLockerNotFoundMeansNoKeyAndPointsAHybridDeviceAtAd()
    {
        var stub = new StubGraphClient(new() { ["informationProtection/bitlocker/recoveryKeys"] = GraphException.NotFound() });
        var model = Model(stub, new FakeOwnerVerifier());
        await model.ShowBitLockerKeysAsync("d1", "o1", "CTS-LT-40487", EntraTrustType.HybridJoined, isEncrypted: true);
        var failed = Assert.IsType<ActionStatus.Failed>(model.Status);
        Assert.StartsWith("No BitLocker recovery key for CTS-LT-40487 is stored in Microsoft Entra. It's hybrid joined", failed.Text, StringComparison.Ordinal);
        Assert.Contains("BitLocker Recovery tab", failed.Text, StringComparison.Ordinal);
        Assert.EndsWith("FieldKit looked for device ID d1 or o1.", failed.Text, StringComparison.Ordinal);
        Assert.Equal(2, stub.Sent.Count);
    }

    [Fact]
    public async Task BitLockerKeysThatVanishAreSkipped()
    {
        var stub = new StubGraphClient(new()
        {
            ["informationProtection/bitlocker/recoveryKeys"] = "bitlocker_keys_list",
            ["informationProtection/bitlocker/recoveryKeys/b7f3a1c2-aaaa-4000-8000-000000000001"] = GraphException.NotFound(),
            ["informationProtection/bitlocker/recoveryKeys/b7f3a1c2-bbbb-4000-8000-000000000002"] = EmptyBody,
        });
        var model = Model(stub, new FakeOwnerVerifier());
        await model.ShowBitLockerKeysAsync("d1", null, "PC");
        Assert.Contains("listed 2", Assert.IsType<ActionStatus.Failed>(model.Status).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnencryptedDeviceSaysSoInsteadOfNoKey()
    {
        var model = Model(new StubGraphClient(new() { ["informationProtection/bitlocker/recoveryKeys"] = "autopilot_empty" }), new FakeOwnerVerifier());
        await model.ShowBitLockerKeysAsync("d1", null, "PC", EntraTrustType.EntraJoined, isEncrypted: false);
        Assert.StartsWith("Intune reports PC isn't encrypted", Assert.IsType<ActionStatus.Failed>(model.Status).Text, StringComparison.Ordinal);

        await model.ShowBitLockerKeysAsync("d1", null, "PC", EntraTrustType.EntraJoined, isEncrypted: true);
        Assert.Contains("BackupToAAD-BitLockerKeyProtector", Assert.IsType<ActionStatus.Failed>(model.Status).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingLapsExplainsWhy()
    {
        var model = Model(new StubGraphClient([]), new FakeOwnerVerifier());
        await model.ShowLocalAdminPasswordAsync("nope", "CTS");
        Assert.Null(model.Revealed);
        var failed = Assert.IsType<ActionStatus.Failed>(model.Status);
        Assert.Contains("No LAPS password", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemediationNeedsHello()
    {
        var script = new AssignedItem { Id = "r-1", Kind = AssignmentKind.Remediation, Name = "Restart spooler" };
        var pc = Device with { DeviceName = "PC1" };
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1/initiateOnDemandProactiveRemediation"] = "" });
        var denied = Model(stub, new FakeOwnerVerifier("Cancelled."));
        Assert.False(await denied.RunRemediationAsync(script, pc));
        Assert.Empty(stub.Sent);

        var verifier = new FakeOwnerVerifier();
        var allowed = Model(stub, verifier);
        Assert.True(await allowed.RunRemediationAsync(script, pc));
        Assert.Equal(["Run Restart spooler on PC1"], verifier.Reasons);
        Assert.Single(stub.Sent);
    }

    [Fact]
    public async Task RemediationForbiddenNamesPermissionAndLicence()
    {
        var script = new AssignedItem { Id = "r-1", Kind = AssignmentKind.Remediation, Name = "Restart spooler" };
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1/initiateOnDemandProactiveRemediation"] = GraphException.Forbidden() });
        var actions = Model(stub, new FakeOwnerVerifier());
        Assert.False(await actions.RunRemediationAsync(script, Device));
        Assert.Equal(new ActionStatus.Failed(GraphFeature.RunRemediation.AccessDeniedMessage()), actions.Status);
    }

    [Fact]
    public void RetireNeedsTheDeviceNameTypedBack()
    {
        Assert.True(DeviceActionsViewModel.RetireNameMatches(" cts-lt-01234 ", "CTS-LT-01234"));
        Assert.False(DeviceActionsViewModel.RetireNameMatches("CTS-LT-0123", "CTS-LT-01234"));
        Assert.False(DeviceActionsViewModel.RetireNameMatches("", ""));
    }

    [Fact]
    public async Task RemediationPickerSortsAndFilters()
    {
        var picker = new RemediationPickerViewModel(new AssignmentCatalog(new Demo.DemoGraphClient(TimeSpan.Zero)), () => { });
        await picker.LoadAsync();
        Assert.Equal(["Clear Teams cache", "Restart print spooler"], picker.Filtered.Select(script => script.Name));
        picker.Query = "SPOOL";
        Assert.Equal(["Restart print spooler"], picker.Filtered.Select(script => script.Name));
    }

    [Fact]
    public void SecretsAreMaskedUntilShownAndCopiedOnlyThroughTheSecureClipboard()
    {
        var credential = new LocalAdminCredential("PC1", "LapsAdmin", "Kp7#vQ2m-Lx9!dRw", DateTimeOffset.UtcNow, null);
        var secrets = new SecretsViewModel(new RevealedSecrets.LocalAdmin(credential));
        Assert.Equal("LAPS password", secrets.Title);
        var password = Assert.Single(secrets.AllSecrets);
        Assert.Equal(new string('•', 16), password.DisplayText);
        Assert.DoesNotContain("Kp7", password.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Kp7", new RevealedSecrets.LocalAdmin(credential).ToString(), StringComparison.Ordinal);

        password.ToggleVisibility();
        Assert.Equal("Kp7#vQ2m-Lx9!dRw", password.DisplayText);
        secrets.MaskAll();
        Assert.False(password.IsVisible);

        var clipboard = new FakeSecureClipboard();
        password.CopyTo(clipboard);
        Assert.Equal(["Kp7#vQ2m-Lx9!dRw"], clipboard.Copied);
        Assert.Equal("Copied: clears in 60 seconds", password.CopyText);
        // No next-rotation row when Entra didn't say.
        Assert.Equal(["Backed up"], secrets.Sections[1].Details.Select(detail => detail.Label));
    }

    [Fact]
    public void BitLockerKeysShowTheKeyIdPrefix()
    {
        var keys = new[] { new BitLockerRecoveryKey("b7f3a1c2-0000-4000", "123456-123456", null, "Operating system volume") };
        var secrets = new SecretsViewModel(new RevealedSecrets.BitLocker("PC1", keys));
        Assert.Equal("BitLocker keys", secrets.Title);
        var section = secrets.Sections[1];
        Assert.Equal("Operating system volume", section.Header);
        Assert.Equal("B7F3A1C2", section.Details[0].Value);
        Assert.Equal("Recovery key", section.Secrets[0].Label);
    }
}
