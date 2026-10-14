using FieldKit.Core.Services;

namespace FieldKit.Core.Tests.Support;

/// <summary>Hands out "cached-token", or "refreshed-token" when forced, and records each request.</summary>
internal sealed class FakeTokenProvider : ITokenProvider
{
    public AuthException? Error { get; set; }

    /// <summary>The <c>forceRefresh</c> value of each request, in order.</summary>
    public List<bool> Requests { get; } = [];

    public Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        Requests.Add(forceRefresh);
        if (Error is not null) return Task.FromException<string>(Error);
        return Task.FromResult(forceRefresh ? "refreshed-token" : "cached-token");
    }
}
