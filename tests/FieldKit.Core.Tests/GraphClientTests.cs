using FieldKit.Core.Tests.Support;
using static FieldKit.Core.Tests.Support.ScriptedTransport;

namespace FieldKit.Core.Tests;

public sealed class GraphClientTests
{
    private static readonly GraphRequest Request = GraphRequest.Get("organization");
    private static readonly byte[] EmptyObject = "{}"u8.ToArray();

    private readonly FakeTokenProvider _tokens = new();
    private readonly List<TimeSpan> _sleeps = [];

    private GraphClient Client(ScriptedTransport transport) =>
        new(_tokens, transport, (duration, _) =>
        {
            _sleeps.Add(duration);
            return Task.CompletedTask;
        });

    private static async Task<GraphException> Fails(Func<Task> action, GraphErrorKind kind)
    {
        var error = await Assert.ThrowsAsync<GraphException>(action);
        Assert.Equal(kind, error.Kind);
        return error;
    }

    [Fact]
    public async Task SendsBearerToken()
    {
        var transport = new ScriptedTransport(Reply.Status(200, EmptyObject));
        await Client(transport).SendAsync(Request);
        Assert.Equal("Bearer cached-token", transport.Sent[0].Headers["Authorization"]);
        Assert.Equal("GET", transport.Sent[0].Method);
    }

    /// <summary>LAPS and BitLocker answer 400 invalid_request without a User-Agent (found on a real tenant).</summary>
    [Fact]
    public async Task SendsAUserAgentWithTheSecretsHeaders()
    {
        var transport = new ScriptedTransport(Reply.Status(200, EmptyObject));
        await Client(transport).SendAsync(GraphRequest.Get("directory/deviceLocalCredentials/d-1", [("$select", "credentials")], RecoverySecrets.ClientHeaders));
        var headers = transport.Sent[0].Headers;
        // The recorder joins parsed header parts with ", "; HttpClient sends them space-separated.
        Assert.Matches(@"^FieldKit/\d+\.\d+\.\d+,? \(Windows \d+\.\d+\.\d+\)$", headers["User-Agent"]);
        Assert.Equal("FieldKit", headers["ocp-client-name"]);
    }

    [Fact]
    public async Task RefreshesOnceOn401ThenRetries()
    {
        var transport = new ScriptedTransport(Reply.Status(401), Reply.Status(200, EmptyObject));
        await Client(transport).SendAsync(Request);
        Assert.Equal([false, true], _tokens.Requests);
        Assert.Equal("Bearer refreshed-token", transport.Sent[^1].Headers["Authorization"]);
    }

    [Fact]
    public async Task SecondUnauthorizedSendsToSignIn()
    {
        var transport = new ScriptedTransport(Reply.Status(401), Reply.Status(401));
        await Fails(() => Client(transport).SendAsync(Request), GraphErrorKind.Unauthorized);
    }

    [Fact]
    public async Task InteractionRequiredSendsToSignIn()
    {
        _tokens.Error = new AuthException(AuthErrorKind.InteractionRequired);
        var transport = new ScriptedTransport();
        await Fails(() => Client(transport).SendAsync(Request), GraphErrorKind.Unauthorized);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task ForbiddenExplainsRolesAfterOneRefresh()
    {
        var body = Fixture.Data("error_forbidden");
        var transport = new ScriptedTransport(Reply.Status(403, body), Reply.Status(403, body));
        await Fails(() => Client(transport).SendAsync(Request), GraphErrorKind.Forbidden);
        Assert.Equal([false, true], _tokens.Requests);
        Assert.Equal("Access denied: check your Intune role / scope tags.", GraphException.Forbidden().UserMessage);
    }

    [Fact]
    public async Task AForbiddenCallStaysForbiddenWhenTheRefreshNeedsTheUser()
    {
        // A permission was added to the app registration: the cached token works, a forced refresh
        // wants interactive sign-in. That's a 403 to explain, not a reason to sign out.
        _tokens.RefreshError = new AuthException(AuthErrorKind.InteractionRequired);
        var body = Fixture.Data("error_forbidden");
        var transport = new ScriptedTransport(Reply.Status(403, body));
        await Fails(() => Client(transport).SendAsync(Request), GraphErrorKind.Forbidden);
        Assert.Equal([false, true], _tokens.Requests);
        Assert.Single(transport.Sent);

        // A 401 still sends to sign-in when the refresh can't happen.
        await Fails(() => Client(new ScriptedTransport(Reply.Status(401))).SendAsync(Request), GraphErrorKind.Unauthorized);
    }

    [Fact]
    public async Task ForbiddenThenGrantedAfterRefresh()
    {
        // An admin granted the permission after the cached token was issued.
        var transport = new ScriptedTransport(Reply.Status(403), Reply.Status(200, EmptyObject));
        await Client(transport).SendAsync(Request);
        Assert.Equal("Bearer refreshed-token", transport.Sent[^1].Headers["Authorization"]);
    }

    [Fact]
    public async Task NotFound()
    {
        var transport = new ScriptedTransport(Reply.Status(404));
        await Fails(() => Client(transport).SendAsync(Request), GraphErrorKind.NotFound);
    }

    [Fact]
    public async Task HonoursRetryAfter()
    {
        var transport = new ScriptedTransport(
            Reply.Status(429, headers: new() { ["Retry-After"] = "5" }),
            Reply.Status(503),
            Reply.Status(200, EmptyObject));
        await Client(transport).SendAsync(Request);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2)], _sleeps);
        Assert.Equal([false, false, false], _tokens.Requests);
    }

    [Fact]
    public async Task GivesUpAfterThreeThrottleRetries()
    {
        var replies = Enumerable.Range(0, 5).Select(_ => Reply.Status(429, headers: new() { ["Retry-After"] = "1" })).ToArray();
        var transport = new ScriptedTransport(replies);
        await Fails(() => Client(transport).SendAsync(Request), GraphErrorKind.Throttled);
        Assert.Equal(3, _sleeps.Count);
        Assert.Equal(4, transport.Sent.Count);
    }

    [Fact]
    public async Task ExcessiveRetryAfterSurfacesImmediately()
    {
        var transport = new ScriptedTransport(Reply.Status(429, headers: new() { ["Retry-After"] = "120" }));
        await Fails(() => Client(transport).SendAsync(Request), GraphErrorKind.Throttled);
    }

    [Fact]
    public async Task EmptyOrNonJsonSuccessIsReportedWithoutTheBody()
    {
        var empty = new ScriptedTransport(Reply.Status(200, headers: new() { ["request-id"] = "abc-123" }));
        var emptyError = await Fails(() => Client(empty).SendAsync(Request), GraphErrorKind.Decoding);
        Assert.Equal("empty response, HTTP 200, request-id abc-123", emptyError.Detail);

        var html = new ScriptedTransport(Reply.Status(200, "<html>secret</html>"u8.ToArray(), new() { ["Content-Type"] = "text/html; charset=utf-8" }));
        var htmlError = await Fails(() => Client(html).SendAsync(Request), GraphErrorKind.Decoding);
        Assert.Equal("not JSON: HTTP 200, text/html, 19 bytes, request-id none", htmlError.Detail);
        Assert.DoesNotContain("secret", htmlError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActionsMayAnswerWithNoBody()
    {
        var transport = new ScriptedTransport(Reply.Status(204));
        var data = await Client(transport).SendAsync(GraphRequest.Post("x"));
        Assert.Empty(data);
    }

    [Fact]
    public async Task NetworkFailure()
    {
        var transport = new ScriptedTransport(Reply.Failure());
        await Fails(() => Client(transport).SendAsync(Request), GraphErrorKind.Network);
    }

    [Fact]
    public async Task ServerErrorCarriesCode()
    {
        var body = """{"error":{"code":"BadRequest","message":"x"}}"""u8.ToArray();
        var transport = new ScriptedTransport(Reply.Status(400, body));
        var error = await Fails(() => Client(transport).SendAsync(Request), GraphErrorKind.Server);
        Assert.True(error.Matches(GraphException.Server(400, "BadRequest")));
    }

    [Fact]
    public async Task FollowsNextLink()
    {
        var transport = new ScriptedTransport(
            Reply.Status(200, Fixture.Data("page1_nextlink")),
            Reply.Status(200, Fixture.Data("page2_last")));
        var items = await Client(transport).GetAllAsync<AutopilotDeviceIdentityDto>(Request);
        Assert.Equal(["ap-p1", "ap-p2"], items.Select(item => item.Id));
        Assert.Contains("skiptoken=abc", transport.Sent[^1].Url.AbsoluteUri, StringComparison.Ordinal);
    }
}
