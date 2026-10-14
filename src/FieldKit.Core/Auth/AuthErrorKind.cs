namespace FieldKit.Core.Auth;

/// <summary>The cases of <see cref="AuthException"/>.</summary>
public enum AuthErrorKind
{
    NotConfigured,
    NoAccount,
    InteractionRequired,
    Cancelled,
    Network,
    Failed,
}
