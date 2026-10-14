using FieldKit.Core.Demo;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows: search, person, permissions, lookup, setup and sign-in view models.</summary>
public sealed class ViewModelTests : IDisposable
{
    private readonly SqliteLocalStore _store = SqliteLocalStore.OpenInMemory();
    private readonly DemoGraphClient _demo = new(TimeSpan.Zero);
    private int _expired;

    public void Dispose() => _store.Dispose();

    private SearchViewModel Search(Graph.IGraphClient? client = null, bool includeGroups = true) =>
        new(new DeviceLookup(client ?? _demo), new DirectoryLookup(client ?? _demo), () => _expired++,
            includeGroups: includeGroups, delay: (_, _) => Task.CompletedTask);

    [Fact]
    public async Task SearchFindsDevicesPeopleAndGroups()
    {
        var search = Search();
        search.Query = "cts";
        await search.CurrentSearch;
        Assert.Equal(8, search.DeviceResults.Count);
        Assert.All(search.DeviceResults, device => Assert.StartsWith("CTS", device.DeviceName, StringComparison.Ordinal));
        Assert.Equal(SectionKind.NotFound, search.People?.Kind);
        Assert.Equal(["CTS Laptops"], search.GroupResults.Select(group => group.DisplayName));
        Assert.False(search.ShowsHint);
    }

    [Fact]
    public async Task ShortQueriesClearResultsAndShowTheHint()
    {
        var search = Search();
        search.Query = "alex";
        await search.CurrentSearch;
        Assert.Equal(["Alex Doe"], search.PeopleResults.Select(person => person.DisplayName));

        search.Query = "a";
        await search.CurrentSearch;
        Assert.Null(search.People);
        Assert.Null(search.Devices);
        Assert.True(search.ShowsHint);
    }

    [Fact]
    public async Task SearchCanLeaveGroupsOut()
    {
        var search = Search(includeGroups: false);
        search.Query = "cts";
        await search.CurrentSearch;
        Assert.Null(search.Groups);
    }

    [Fact]
    public async Task TypingCancelsTheSearchBeforeIt()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var search = new SearchViewModel(new DeviceLookup(_demo), new DirectoryLookup(_demo), () => { },
            delay: async (_, token) =>
            {
                // Only the first search waits; it's cancelled by the second keystroke.
                if (Interlocked.Increment(ref calls) == 1) await gate.Task.WaitAsync(token);
            });
        search.Query = "alex";
        var first = search.CurrentSearch;
        search.Query = "sam";
        await search.CurrentSearch;
        gate.SetResult();
        await first;
        Assert.Equal(["Sam Lee"], search.PeopleResults.Select(person => person.DisplayName));
    }

    [Fact]
    public void SerialQueriesOfferADirectLookup()
    {
        var search = Search();
        search.Query = " sn: 5cg1234xyz";
        Assert.Equal("5CG1234XYZ", search.SerialSuggestion);
        search.Query = "alex doe";
        Assert.Null(search.SerialSuggestion);
    }

    [Fact]
    public async Task PeopleSearchFailureNamesItsPermissionWhileDevicesStillLoad()
    {
        var stub = new StubGraphClient(new()
        {
            ["users"] = GraphException.Forbidden(),
            ["deviceManagement/managedDevices"] = "managedDevices_single",
            ["groups"] = "member_of",
        });
        var search = Search(stub);
        search.Query = "cts";
        await search.CurrentSearch;
        Assert.Equal(GraphFeature.PeopleSearch.AccessDeniedMessage(), search.People?.Message);
        Assert.Single(search.DeviceResults);
    }

    [Fact]
    public async Task PersonDevicesNewestSyncFirst()
    {
        var stub = new StubGraphClient(new() { ["users/u-1/managedDevices"] = "managedDevices_duplicate" });
        var person = new PersonViewModel(new DirectoryUser { Id = "u-1", DisplayName = "Sam", UserPrincipalName = "sam@contoso.com", Mail = "SAM@contoso.com" }, new DirectoryLookup(stub), () => { });
        await person.LoadAsync();
        Assert.Equal(["DESKTOP-7Q2LMNP", "CTS-DT-00042"], person.DeviceList.Select(device => device.DeviceName));
        Assert.False(person.ShowsEmail);
    }

    [Fact]
    public async Task PersonWithNoDevices()
    {
        var stub = new StubGraphClient(new() { ["users/u-1/managedDevices"] = "managedDevices_empty" });
        var person = new PersonViewModel(new DirectoryUser { Id = "u-1" }, new DirectoryLookup(stub), () => { });
        await person.LoadAsync();
        Assert.Equal(SectionKind.NotFound, person.Devices.Kind);
    }

    [Fact]
    public async Task PermissionsShowWhatTheTokenCarries()
    {
        var auth = new FakeAuthProvider { Scopes = ["https://graph.microsoft.com/User.Read", "openid"] };
        var permissions = new PermissionsViewModel(auth);
        await permissions.LoadAsync();
        Assert.Equal(PermissionsState.Loaded, permissions.State);
        Assert.True(permissions.Rows.Single(row => row.Permission.Name == "User.Read").IsGranted);
        Assert.Equal("Missing", permissions.Rows.Single(row => row.Permission.Name == "BitlockerKey.Read.All").Status);
        Assert.Null(permissions.RefreshedAt);

        await permissions.RefreshCommand.ExecuteAsync(null);
        Assert.Equal([false, true], auth.ScopeRequests);
        Assert.NotNull(permissions.RefreshedAt);
    }

    [Fact]
    public async Task PermissionsUnknownOrFailed()
    {
        var unknown = new PermissionsViewModel(new FakeAuthProvider { Scopes = ["https://graph.microsoft.com/.default"] });
        await unknown.LoadAsync();
        Assert.Equal(PermissionsState.Unknown, unknown.State);

        var failed = new PermissionsViewModel(new FakeAuthProvider { ScopesError = new AuthException(AuthErrorKind.InteractionRequired) });
        await failed.LoadAsync();
        Assert.Equal(PermissionsState.Failed, failed.State);
        Assert.Equal("Sign in to continue.", failed.Message);
    }

    private LookupViewModel Lookup(ILocalDeviceInfo? device = null) =>
        new(new AppSession(_store, _ => new FakeAuthProvider()), _store, device ?? new FakeDeviceInfo("5CG1234XYZ"));

    [Fact]
    public void ThisPcIsOfferedOnlyForARealSerial()
    {
        Assert.Equal("5CG1234XYZ", Lookup().ThisPcSerial);
        Assert.Null(Lookup(new FakeDeviceInfo("To be filled by O.E.M.")).ThisPcSerial);
        Assert.Equal("DESKTOP-TEST", Lookup().ThisPcName);
    }

    [Fact]
    public void LookupShowsRecents()
    {
        var lookup = Lookup();
        Assert.Empty(lookup.Recents);
        _store.RecordLookup("PF2ABCDE", "DESKTOP-7Q2LMNP", DateTimeOffset.UtcNow);
        lookup.Refresh();
        Assert.Equal("PF2ABCDE", Assert.Single(lookup.Recents).Serial);
    }

    [Fact]
    public void SetupValidatesBeforeSaving()
    {
        var session = new AppSession(_store, config => new FakeAuthProvider { IsConfigured = config is not null });
        var setup = new SetupViewModel(session);
        Assert.False(setup.CanSave);
        Assert.False(setup.CanCancel);
        Assert.Contains("<client ID>", setup.Guide.RedirectUri, StringComparison.Ordinal);

        setup.TenantId = "common";
        setup.ClientId = "1A2B3C4D-1111-2222-3333-444455556666";
        Assert.EndsWith("/1a2b3c4d-1111-2222-3333-444455556666", setup.Guide.RedirectUri, StringComparison.Ordinal);
        setup.SaveCommand.Execute(null);
        Assert.Equal(AuthConfigProblem.MultiTenantAuthority.Message(), setup.Message);

        setup.TenantId = "contoso.onmicrosoft.com";
        Assert.Null(setup.Message);
        setup.SaveCommand.Execute(null);
        Assert.Equal(SessionState.SignedOut, session.State);
        Assert.NotNull(AuthConfigStore.Saved(_store));
        setup.Detach();
    }

    [Fact]
    public async Task SignInTextFollowsTheSession()
    {
        var session = new AppSession(_store, config => new FakeAuthProvider { IsConfigured = config is not null });
        var signIn = new SignInViewModel(session);
        Assert.False(signIn.CanSignIn);
        Assert.Equal("Tenant setup", signIn.SetupLinkText);

        session.ApplySetup(new AuthConfig("1a2b3c4d-1111-2222-3333-444455556666", "contoso.onmicrosoft.com"));
        Assert.Equal("Tenant: contoso.onmicrosoft.com", signIn.SetupLinkText);

        await session.StartDemoAsync();
        Assert.Equal("Enter demo", signIn.ButtonText);
        Assert.Equal("Leave demo and set up a tenant", signIn.SetupLinkText);
        signIn.Detach();
    }

    [Fact]
    public void SettingsClampTheStaleThreshold()
    {
        var settings = new SettingsStore(_store);
        var model = new SettingsViewModel(new AppSession(_store, _ => new FakeAuthProvider()), settings, _store, "0.1.0");
        model.StaleThresholdDays = 9000;
        Assert.Equal(AuditSettings.MaxStaleThresholdDays, settings.StaleThresholdDays);
        model.StaleThresholdDays = double.NaN;
        Assert.Equal(AuditSettings.DefaultStaleThresholdDays, settings.StaleThresholdDays);
        settings.NamingConventionRegex = "([";
        Assert.NotNull(model.RegexProblem);
        Assert.Null(model.Permissions); // nobody is signed in

        model.ClearAllLocalDataCommand.Execute(null);
        Assert.Equal("Local data cleared.", model.ClearMessage);
        Assert.Equal("", settings.NamingConventionRegex);
        model.Detach();
    }
}
