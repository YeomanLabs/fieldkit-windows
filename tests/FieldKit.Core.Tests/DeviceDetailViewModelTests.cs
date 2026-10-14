using FieldKit.Core.Demo;
using FieldKit.Core.Persistence;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows: the device page's view model (iOS tests this model only through the UI).</summary>
public sealed class DeviceDetailViewModelTests : IDisposable
{
    private readonly SqliteLocalStore _store = SqliteLocalStore.OpenInMemory();
    private readonly SettingsStore _settings;
    private int _expired;

    public DeviceDetailViewModelTests()
    {
        _settings = new SettingsStore(_store);
    }

    public void Dispose() => _store.Dispose();

    private DeviceDetailViewModel Model(string serial, Graph.IGraphClient client, string? preferredIntuneId = null) =>
        new(serial, preferredIntuneId, new DeviceLookup(client), _settings, _store, () => _expired++);

    [Fact]
    public async Task HealthyDeviceLoadsEverySectionAndIsRecorded()
    {
        var model = Model("5CG1234XYZ", new DemoGraphClient(TimeSpan.Zero));
        await model.LoadAsync();
        Assert.Equal(SectionKind.Loaded, model.Intune.Kind);
        Assert.Equal(SectionKind.Loaded, model.Entra.Kind);
        Assert.Equal(SectionKind.Loaded, model.Autopilot.Kind);
        Assert.Equal(SectionKind.Loaded, model.Compliance.Kind);
        Assert.Empty(model.Flags);
        Assert.False(model.IsLoading);
        Assert.False(model.HasFailedSection);
        Assert.Equal("CTS-LT-01234", model.Title);
        Assert.Equal("5CG1234XYZ · Windows 10.0.26100.4652", model.Subtitle);
        Assert.Equal("c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b", model.EntraDeviceId);
        Assert.Equal("e-1", model.EntraObjectId);

        var recent = Assert.Single(_store.RecentLookups());
        Assert.Equal(("5CG1234XYZ", "CTS-LT-01234"), (recent.Serial, recent.DeviceName));
    }

    [Fact]
    public async Task AutopilotOnlyDeviceSkipsTheIntuneSections()
    {
        var model = Model("PF9NEW01", new DemoGraphClient(TimeSpan.Zero));
        await model.LoadAsync();
        Assert.Equal(SectionKind.NotFound, model.Intune.Kind);
        Assert.Equal(SectionKind.Skipped, model.Entra.Kind);
        Assert.Equal("Not enrolled in Intune.", model.Compliance.Message);
        Assert.Equal([FlagKind.NotInIntune], model.Flags.Select(flag => flag.Kind));
        Assert.Equal("PF9NEW01 · HP EliteBook 840 G11", model.Subtitle);
        Assert.Equal("PF9NEW01", model.Title);
    }

    [Fact]
    public async Task DuplicatesCanBeSwitchedAndReloadTheRecordDetails()
    {
        var model = Model("PF2ABCDE", new DemoGraphClient(TimeSpan.Zero));
        await model.LoadAsync();
        Assert.True(model.HasDuplicateRecords);
        Assert.Equal("i-2", model.SelectedIntune?.Id);
        Assert.Contains(FlagKind.EntraDisabled, model.Flags.Select(flag => flag.Kind));

        await model.SelectIntuneRecordAsync("i-3");
        Assert.Equal("CTS-DT-00042", model.Title);
        // The second record has the all-zero Entra ID, so Entra is skipped and its flags go.
        Assert.Equal(SectionKind.Skipped, model.Entra.Kind);
        Assert.DoesNotContain(FlagKind.EntraDisabled, model.Flags.Select(flag => flag.Kind));
        Assert.Contains(FlagKind.DuplicateIntuneRecords, model.Flags.Select(flag => flag.Kind));
        Assert.Equal(SectionKind.Loaded, model.Compliance.Kind);
        Assert.False(model.HasStorage);
    }

    [Fact]
    public async Task APreferredRecordIsShownFirst()
    {
        var model = Model("PF2ABCDE", new DemoGraphClient(TimeSpan.Zero), preferredIntuneId: "i-3");
        await model.LoadAsync();
        Assert.Equal("i-3", model.SelectedIntune?.Id);
    }

    [Fact]
    public async Task ForbiddenIntuneFailsThatSectionAndSkipsItsDependants()
    {
        var model = Model("DENIED01", new DemoGraphClient(TimeSpan.Zero));
        await model.LoadAsync();
        Assert.Equal(SectionKind.Failed, model.Intune.Kind);
        Assert.Equal(GraphException.Forbidden().UserMessage, model.Intune.Message);
        Assert.Equal(SectionKind.Skipped, model.Entra.Kind);
        // Autopilot still loads on its own.
        Assert.Equal(SectionKind.NotFound, model.Autopilot.Kind);
        // No flags, but that isn't a clean bill of health.
        Assert.Empty(model.Flags);
        Assert.True(model.HasFailedSection);
    }

    [Fact]
    public async Task ComplianceFailureNamesItsPermission()
    {
        var stub = new StubGraphClient(new()
        {
            ["deviceManagement/managedDevices"] = "managedDevices_single",
            ["devices"] = "devices_entra",
            ["deviceManagement/windowsAutopilotDeviceIdentities"] = "autopilot_identities",
            ["deviceManagement/managedDevices/6b9f2c1e-8a5d-4f3b-9c2e-1d7a4b8e0f11/deviceCompliancePolicyStates"] = GraphException.Forbidden(),
        });
        var model = Model("5CG1234XYZ", stub);
        await model.LoadAsync();
        Assert.Equal(GraphFeature.ComplianceDetails.AccessDeniedMessage(), model.Compliance.Message);
        Assert.Equal(SectionKind.Loaded, model.Entra.Kind);
    }

    [Fact]
    public async Task UnauthorizedEndsTheSessionInsteadOfShowingAnError()
    {
        var stub = new StubGraphClient(new()
        {
            ["deviceManagement/managedDevices"] = GraphException.Unauthorized(),
            ["deviceManagement/windowsAutopilotDeviceIdentities"] = "autopilot_empty",
        });
        var model = Model("5CG1234XYZ", stub);
        await model.LoadAsync();
        Assert.Equal(1, _expired);
        Assert.Equal(SectionKind.Loading, model.Intune.Kind);
    }

    [Fact]
    public async Task FlagsFollowTheAuditRules()
    {
        var model = Model("5CG1234XYZ", new DemoGraphClient(TimeSpan.Zero));
        await model.LoadAsync();
        var changed = new List<string?>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _settings.NamingConventionRegex = "DESK-.*";
        Assert.Contains(nameof(DeviceDetailViewModel.Flags), changed);
        Assert.Contains(FlagKind.NameConvention, model.Flags.Select(flag => flag.Kind));
        model.Detach();
    }
}
