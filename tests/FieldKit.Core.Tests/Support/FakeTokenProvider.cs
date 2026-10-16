using FieldKit.Core.Services;

namespace FieldKit.Core.Tests.Support;

/// <summary>Hands out "cached-token", or "refreshed-token" when forced, and records each request.</summary>
internal sealed class FakeTokenProvider : ITokenProvider
{
    public AuthException? Error { get; set; }

    /// <summary>Thrown only for a forced refresh (the cached token still works).</summary>
    public AuthException? RefreshError { get; set; }

    /// <summary>The <c>forceRefresh</c> value of each request, in order.</summary>
    public List<bool> Requests { get; } = [];

    public Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        Requests.Add(forceRefresh);
        if (Error is not null) return Task.FromException<string>(Error);
        if (forceRefresh && RefreshError is not null) return Task.FromException<string>(RefreshError);
        return Task.FromResult(forceRefresh ? "refreshed-token" : "cached-token");
    }
}
