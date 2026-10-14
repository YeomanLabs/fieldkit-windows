using FieldKit.Core.Auth;

namespace FieldKit.Core.Services;

/// <summary>Sign-in for a tenant or for demo mode (ports <c>AuthProviding</c>).</summary>
public interface IAuthProvider : ITokenProvider
{
    bool IsConfigured { get; }

    /// <summary>The cached account, if there is one and it doesn't need interactive sign-in.</summary>
    Task<SignedInUser?> RestoreSessionAsync(CancellationToken cancellationToken = default);

    Task<SignedInUser> SignInAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The Graph permissions the current token carries. <paramref name="forceRefresh"/> fetches
    /// a new token, picking up consent granted since sign-in.
    /// </summary>
    Task<IReadOnlyList<string>> GrantedScopesAsync(bool forceRefresh, CancellationToken cancellationToken = default);
}
