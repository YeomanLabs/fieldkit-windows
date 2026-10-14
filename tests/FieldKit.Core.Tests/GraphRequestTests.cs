using System.Text;
using FieldKit.Core.Tests.Support;
using static FieldKit.Core.Tests.Support.ScriptedTransport;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows: URL building, body encoding, and keeping the token on Graph.</summary>
public sealed class GraphRequestTests
{
    [Fact]
    public void PathSegmentsAreEscaped()
    {
        // Guest UPNs contain '#', which would otherwise start a fragment and drop the rest of the path.
        var request = GraphRequest.Get("users/alex_contoso.com#EXT#@tenant.onmicrosoft.com/transitiveMemberOf");
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users/alex_contoso.com%23EXT%23@tenant.onmicrosoft.com/transitiveMemberOf",
            request.Url.AbsoluteUri);
    }

    [Fact]
    public void QueryValuesCantBreakOutOfTheirParameter()
    {
        var request = GraphRequest.Get("users", [("$search", "\"displayName:a&b=c+d\"")]);
        Assert.Equal("?$search=%22displayName:a%26b%3Dc%2Bd%22", request.Url.Query);
        Assert.Equal("\"displayName:a&b=c+d\"", RequestInspection.QueryValue(request.Url, "$search"));
    }

    [Fact]
    public void PostBodiesSortKeysAndKeepTextReadable()
    {
        var request = GraphRequest.Post("x", new Dictionary<string, string> { ["b"] = "https://a/b", ["a"] = "Zoë & co" });
        Assert.Equal("""{"a":"Zoë & co","b":"https://a/b"}""", Encoding.UTF8.GetString(request.Body!));
    }

    [Fact]
    public async Task NextLinkOutsideGraphIsNotFollowed()
    {
        var page = """{"value":[{"id":"ap-1"}],"@odata.nextLink":"https://attacker.example/steal?$skiptoken=1"}"""u8.ToArray();
        var transport = new ScriptedTransport(Reply.Status(200, page), Reply.Status(200, page));
        var client = new GraphClient(new FakeTokenProvider(), transport, (_, _) => Task.CompletedTask);
        var error = await Assert.ThrowsAsync<GraphException>(() =>
            client.GetAllAsync<AutopilotDeviceIdentityDto>(GraphRequest.Get("deviceManagement/windowsAutopilotDeviceIdentities")));
        Assert.Equal(GraphErrorKind.Decoding, error.Kind);
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task ClientOnlySendsTheTokenToGraph()
    {
        var tokens = new FakeTokenProvider();
        var transport = new ScriptedTransport(Reply.Status(200, "{}"u8.ToArray()));
        var client = new GraphClient(tokens, transport, (_, _) => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendAsync(GraphRequest.Get(new Uri("https://attacker.example/v1.0/organization"))));
        Assert.Empty(tokens.Requests);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task CancellationIsNotReportedAsANetworkError()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var transport = new ScriptedTransport(Reply.Status(200, "{}"u8.ToArray()));
        var client = new GraphClient(new CancellingTokenProvider(), transport, (_, _) => Task.CompletedTask);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(GraphRequest.Get("organization"), cancellation.Token));
    }

    [Theory]
    [InlineData("5", 0, 5)]
    [InlineData(" 7 ", 2, 7)]
    [InlineData(null, 0, 1)]
    [InlineData(null, 2, 4)]
    [InlineData("Wed, 21 Oct 2026 07:28:00 GMT", 1, 2)]
    [InlineData("-3", 1, 2)]
    public void RetryDelayPrefersTheHeader(string? header, int attempt, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), GraphClient.RetryDelay(header, attempt));
    }

    /// <summary>A token provider that honours cancellation, as MSAL does.</summary>
    private sealed class CancellingTokenProvider : Services.ITokenProvider
    {
        public Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("token");
        }
    }
}
