using FieldKit.Core.Services;

namespace FieldKit.Core.Tests.Support;

/// <summary>Sign-in that does what each test scripts, standing in for MSAL.</summary>
internal sealed class FakeAuthProvider : IAuthProvider
{
    public static readonly SignedInUser Alex = new("Alex Doe", "alex.doe@contoso.com", "11111111-2222-3333-4444-555555555555");

    public bool IsConfigured { get; init; } = true;

    /// <summary>What restoring a session finds.</summary>
    public SignedInUser? CachedUser { get; set; }

    /// <summary>Thrown by sign-in; when null, sign-in succeeds as <see cref="Alex"/>.</summary>
    public AuthException? SignInError { get; set; }

    public IReadOnlyList<string> Scopes { get; set; } = [];

    public AuthException? ScopesError { get; set; }

    public int SignOuts { get; private set; }

    public List<bool> ScopeRequests { get; } = [];

    public Task<SignedInUser?> RestoreSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(CachedUser);

    public Task<SignedInUser> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (SignInError is not null) return Task.FromException<SignedInUser>(SignInError);
        CachedUser = Alex;
        return Task.FromResult(Alex);
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        SignOuts++;
        CachedUser = null;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GrantedScopesAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        ScopeRequests.Add(forceRefresh);
        return ScopesError is null ? Task.FromResult(Scopes) : Task.FromException<IReadOnlyList<string>>(ScopesError);
    }

    public Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default) =>
        Task.FromResult("token");
}
