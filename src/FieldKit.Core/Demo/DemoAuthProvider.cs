using FieldKit.Core.Auth;
using FieldKit.Core.Services;

namespace FieldKit.Core.Demo;

/// <summary>
/// Sign-in for demo mode (ports <c>DemoAuthService</c>): sample data with no tenant, for trying
/// FieldKit and for Store certification. Started from the setup page, or with <c>--demo</c>.
/// </summary>
public sealed class DemoAuthProvider : IAuthProvider
{
    private static readonly SignedInUser DemoUser = new("Demo Engineer", "demo@contoso.com", "demo");

    private volatile bool _signedIn;

    public bool IsConfigured => true;

    public Task<SignedInUser?> RestoreSessionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_signedIn ? DemoUser : null);

    public Task<SignedInUser> SignInAsync(CancellationToken cancellationToken = default)
    {
        _signedIn = true;
        return Task.FromResult(DemoUser);
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        _signedIn = false;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GrantedScopesAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        if (!_signedIn) return Task.FromException<IReadOnlyList<string>>(new AuthException(AuthErrorKind.NoAccount));
        IReadOnlyList<string> scopes = GraphPermission.All.Select(permission => $"https://graph.microsoft.com/{permission.Name}").ToList();
        return Task.FromResult(scopes);
    }

    public Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default) =>
        _signedIn
            ? Task.FromResult("demo-token")
            : Task.FromException<string>(new AuthException(AuthErrorKind.NoAccount));
}
