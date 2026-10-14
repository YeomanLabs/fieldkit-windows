namespace FieldKit.Core.Services;

public interface ITokenProvider
{
    /// <summary>
    /// A Graph access token for the signed-in account, refreshed silently. Throws
    /// <see cref="Auth.AuthException"/> (<see cref="Auth.AuthErrorKind.InteractionRequired"/>) when
    /// the user must sign in again.
    /// </summary>
    Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default);
}
