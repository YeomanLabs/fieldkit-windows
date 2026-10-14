using FieldKit.Core.Demo;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows: the Devices, Users and Groups tabs, which list the tenant before anything is typed.</summary>
public sealed class BrowseViewModelTests
{
    private const string NextGroups = "https://graph.microsoft.com/v1.0/groups?$skiptoken=page2";
    private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;
    private readonly DemoGraphClient _demo = new(TimeSpan.Zero);
    private int _expired;

    private GroupListViewModel Groups(IGraphClient client) => new(new DirectoryLookup(client), () => _expired++, NoDelay);

    private UserListViewModel Users() => new(new DirectoryLookup(_demo), () => _expired++, NoDelay);

    private DeviceListViewModel Devices(Func<TimeSpan, CancellationToken, Task>? delay = null) =>
        new(new DeviceLookup(_demo), () => _expired++, delay ?? NoDelay);

    private static ScriptedGraphClient TwoPagesOfGroups(Exception? secondPageError = null) => new(request =>
        request.Url.Query.Contains("skiptoken", StringComparison.Ordinal)
            ? secondPageError is null ? ScriptedGraphClient.Page(["Gamma"]) : throw secondPageError
            : ScriptedGraphClient.Page(["Alpha", "Beta"], NextGroups));

    [Fact]
    public async Task ListsTheFirstPageAToZThenLoadsMore()
    {
        var client = TwoPagesOfGroups();
        var groups = Groups(client);
        await groups.EnsureLoadedAsync();
        Assert.Equal(["Alpha", "Beta"], groups.Items.Select(group => group.DisplayName));
        Assert.True(groups.CanLoadMore);
        Assert.Equal("All groups (2 so far)", groups.Heading);
        var first = Uri.UnescapeDataString(client.Sent[0].Url.Query);
        Assert.Contains("$orderby=displayName", first, StringComparison.Ordinal);
        Assert.Contains($"$top={DirectoryLookup.PageSize}", first, StringComparison.Ordinal);

        await groups.LoadMoreAsync();
        Assert.Equal(["Alpha", "Beta", "Gamma"], groups.Items.Select(group => group.DisplayName));
        Assert.False(groups.HasMore);
        Assert.False(groups.CanLoadMore);
        Assert.Equal("All groups (3)", groups.Heading);
        Assert.Equal(new Uri(NextGroups), client.Sent[1].Url);
    }

    [Fact]
    public async Task ReturningToTheTabKeepsWhatsLoaded()
    {
        var client = TwoPagesOfGroups();
        var groups = Groups(client);
        await groups.EnsureLoadedAsync();
        await groups.LoadMoreAsync();
        await groups.EnsureLoadedAsync();
        Assert.Equal(2, client.Sent.Count);
        Assert.Equal(3, groups.Items.Count);

        await groups.RefreshAsync();
        Assert.Equal(3, client.Sent.Count);
        Assert.Equal(2, groups.Items.Count);
    }

    [Fact]
    public async Task AFailedNextPageKeepsTheRowsShown()
    {
        var groups = Groups(TwoPagesOfGroups(GraphException.Throttled()));
        await groups.EnsureLoadedAsync();
        await groups.LoadMoreAsync();
        Assert.Equal(2, groups.Items.Count);
        Assert.Equal(SectionKind.Loaded, groups.State.Kind);
        Assert.Equal(GraphFeature.Groups.Message(GraphException.Throttled()), groups.LoadMoreError);
        Assert.True(groups.CanLoadMore);
    }

    [Fact]
    public async Task AFailedFirstPageNamesTheMissingPermission()
    {
        var groups = Groups(new ScriptedGraphClient(_ => throw GraphException.Forbidden()));
        await groups.EnsureLoadedAsync();
        Assert.Equal(SectionKind.Failed, groups.State.Kind);
        Assert.Equal(GraphFeature.Groups.AccessDeniedMessage(), groups.State.Message);
        Assert.Empty(groups.Items);
    }

    [Fact]
    public async Task AnExpiredSessionGoesBackToSignIn()
    {
        var groups = Groups(new ScriptedGraphClient(_ => throw GraphException.Unauthorized()));
        await groups.EnsureLoadedAsync();
        Assert.Equal(1, _expired);
        Assert.True(groups.State.IsLoading);
    }

    [Fact]
    public async Task TypingSearchesAndClearingTheBoxListsEveryoneAgain()
    {
        var users = Users();
        await users.EnsureLoadedAsync();
        Assert.Equal(["Alex Doe", "Casey Nguyen", "Jordan Kim", "Morgan Reyes", "Priya Patel", "Sam Lee"], users.Items.Select(user => user.DisplayName));
        Assert.Equal("All users (6)", users.Heading);

        users.Query = "alex";
        await users.CurrentLoad;
        Assert.Equal(["Alex Doe"], users.Items.Select(user => user.DisplayName));
        Assert.Equal("People matching “alex” (1)", users.Heading);
        Assert.False(users.CanLoadMore);

        users.Query = "";
        await users.CurrentLoad;
        Assert.Equal(6, users.Items.Count);
    }

    [Fact]
    public async Task DemoSearchMatchesTheStartOfAWord()
    {
        var users = Users();
        users.Query = "pri";
        await users.CurrentLoad;
        Assert.Equal(["Priya Patel"], users.Items.Select(user => user.DisplayName));
        users.Query = "patel";
        await users.CurrentLoad;
        Assert.Equal(["Priya Patel"], users.Items.Select(user => user.DisplayName));

        var groups = Groups(_demo);
        groups.Query = "kio";
        await groups.CurrentLoad;
        Assert.Equal(["Kiosk Devices"], groups.Items.Select(group => group.DisplayName));
    }

    [Fact]
    public async Task OneLetterDoesntSearch()
    {
        var users = Users();
        await users.EnsureLoadedAsync();
        users.Query = "a";
        await users.CurrentLoad;
        Assert.False(users.IsSearching);
        Assert.Equal(6, users.Items.Count);
    }

    [Fact]
    public async Task NoMatchesSaySo()
    {
        var groups = Groups(_demo);
        groups.Query = "zzz";
        await groups.CurrentLoad;
        Assert.Equal(SectionKind.NotFound, groups.State.Kind);
        Assert.Equal(groups.NoMatchText, groups.NotFoundText);
        Assert.Equal("Groups matching “zzz”", groups.Heading);
    }

    [Fact]
    public async Task TypingCancelsTheSearchBeforeIt()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var users = new UserListViewModel(new DirectoryLookup(_demo), () => { }, async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) == 1) await gate.Task.WaitAsync(token);
        });
        users.Query = "alex";
        var first = users.CurrentLoad;
        users.Query = "sam";
        await users.CurrentLoad;
        gate.SetResult();
        await first;
        Assert.Equal(["Sam Lee"], users.Items.Select(user => user.DisplayName));
    }

    [Fact]
    public async Task DevicesListEveryIntuneDeviceByName()
    {
        var devices = Devices();
        await devices.EnsureLoadedAsync();
        var names = devices.Items.Select(device => device.DeviceName).ToList();
        Assert.Equal(10, names.Count);
        Assert.Equal(names.Order(StringComparer.OrdinalIgnoreCase), names);
        Assert.False(devices.HasMore);
    }

    [Fact]
    public async Task ASerialFindsItsDeviceAlongsideNameMatches()
    {
        var devices = Devices();
        devices.Query = "5cg2301abc";
        await devices.CurrentLoad;
        Assert.Equal(["CTS-LT-01301"], devices.Items.Select(device => device.DeviceName));

        devices.Query = "cts-lt";
        await devices.CurrentLoad;
        Assert.Equal(4, devices.Items.Count);
        Assert.Equal(devices.Items.Count, devices.Items.DistinctBy(device => device.Id).Count());
    }

    [Fact]
    public async Task EnterOpensTheDeviceNamedExactly()
    {
        var devices = Devices();
        devices.Query = "cts-lt-01301";
        await devices.CurrentLoad;
        Assert.Equal(new DeviceTarget("5CG2301ABC", "i-4"), devices.EnterTarget());
    }

    [Fact]
    public void EnterOpensAScannedSerialBeforeTheSearchRuns()
    {
        var devices = Devices(delay: (_, token) => Task.Delay(Timeout.Infinite, token));
        devices.Query = "sn: 5cg1234xyz";
        Assert.Empty(devices.Items);
        Assert.Equal(new DeviceTarget("5CG1234XYZ", null), devices.EnterTarget());
    }

    [Fact]
    public void EnterNormalisesWhatWasTypedOrScannedAndClearsTheBox()
    {
        var devices = Devices(delay: (_, token) => Task.Delay(Timeout.Infinite, token));
        devices.Query = "*s/n 5cg1234xyz*";
        Assert.Equal(new DeviceTarget("5CG1234XYZ", null), devices.Submit());
        Assert.Equal("", devices.Query);
        Assert.Null(devices.EnterError);

        devices.Query = "PO#8891/22";
        Assert.Null(devices.Submit());
        Assert.NotNull(devices.EnterError);
        devices.Query = "5CG";
        Assert.Null(devices.EnterError);

        // Enter on an empty box does nothing and says nothing.
        devices.Query = "  ";
        Assert.Null(devices.Submit());
        Assert.Null(devices.EnterError);
    }

    [Fact]
    public async Task ChoosingTheTabFromTheMenuStartsOverFromGraph()
    {
        var client = new ScriptedGraphClient(request => request.Url.Query.Contains("skiptoken", StringComparison.Ordinal)
            ? ScriptedGraphClient.Page(["Gamma"])
            : request.Url.Query.Contains("search", StringComparison.Ordinal)
                ? ScriptedGraphClient.Page(["Beta"])
                : ScriptedGraphClient.Page(["Alpha", "Beta"], NextGroups));
        var groups = Groups(client);
        await groups.EnsureLoadedAsync();
        await groups.LoadMoreAsync();
        groups.Query = "be";
        await groups.CurrentLoad;

        await groups.StartOverAsync();
        Assert.Equal("", groups.Query);
        Assert.False(groups.IsSearching);
        // The first page again from Graph, not the three rows browsed before the search.
        Assert.Equal(["Alpha", "Beta"], groups.Items.Select(group => group.DisplayName));
        Assert.Equal(4, client.Sent.Count);
        Assert.DoesNotContain("search", client.Sent[3].Url.Query, StringComparison.Ordinal);
        Assert.Equal("All groups (2 so far)", groups.Heading);
        Assert.True(groups.CanLoadMore);
    }

    [Fact]
    public async Task StartingOverTwiceAtOnceAsksGraphOnce()
    {
        var hold = new TaskCompletionSource();
        var client = new ScriptedGraphClient(_ => ScriptedGraphClient.Page(["Alpha"]), hold.Task);
        var groups = Groups(client);
        var first = groups.StartOverAsync();
        var second = groups.StartOverAsync();
        Assert.Same(first, second);
        hold.SetResult();
        await second;
        Assert.Single(client.Sent);
        Assert.Equal(["Alpha"], groups.Items.Select(group => group.DisplayName));

        // Once that's done, the menu item asks again.
        await groups.StartOverAsync();
        Assert.Equal(2, client.Sent.Count);
    }

    [Fact]
    public async Task StartingOverClearsTheDevicesEnterError()
    {
        var devices = Devices(delay: (_, token) => Task.Delay(Timeout.Infinite, token));
        devices.Query = "PO#8891/22";
        Assert.Null(devices.Submit());
        Assert.NotNull(devices.EnterError);
        await devices.StartOverAsync();
        Assert.Null(devices.EnterError);
        Assert.Equal(10, devices.Items.Count);
    }

    [Fact]
    public async Task ClearingASearchPutsTheListBackWithoutAskingGraphAgain()
    {
        var client = new ScriptedGraphClient(request => request.Url.Query.Contains("skiptoken", StringComparison.Ordinal)
            ? ScriptedGraphClient.Page(["Gamma"])
            : request.Url.Query.Contains("search", StringComparison.Ordinal)
                ? ScriptedGraphClient.Page(["Beta"])
                : ScriptedGraphClient.Page(["Alpha", "Beta"], NextGroups));
        var groups = Groups(client);
        await groups.EnsureLoadedAsync();
        groups.Query = "be";
        await groups.CurrentLoad;
        Assert.Equal(["Beta"], groups.Items.Select(group => group.DisplayName));
        Assert.False(groups.CanLoadMore);

        groups.Query = "";
        await groups.CurrentLoad;
        Assert.Equal(["Alpha", "Beta"], groups.Items.Select(group => group.DisplayName));
        Assert.Equal(2, client.Sent.Count);
        Assert.True(groups.CanLoadMore);
        await groups.LoadMoreAsync();
        Assert.Equal(3, groups.Items.Count);
        Assert.Equal(new Uri(NextGroups), client.Sent[2].Url);
    }
}
