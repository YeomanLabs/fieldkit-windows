namespace FieldKit.Core.Auth;

/// <summary>A sign-in or token failure (ports <c>AuthError.swift</c>).</summary>
public sealed class AuthException : Exception
{
    public AuthException(AuthErrorKind kind, string? detail = null)
        : base(UserMessageFor(kind, detail))
    {
        Kind = kind;
        Detail = detail;
    }

    public AuthErrorKind Kind { get; }

    /// <summary>MSAL's description, for <see cref="AuthErrorKind.Failed"/>.</summary>
    public string? Detail { get; }

    public string UserMessage => Message;

    private static string UserMessageFor(AuthErrorKind kind, string? detail) =>
        kind switch
        {
            AuthErrorKind.NotConfigured => "FieldKit isn't set up for a tenant yet. Open Tenant setup and enter your IDs.",
            AuthErrorKind.NoAccount or AuthErrorKind.InteractionRequired => "Sign in to continue.",
            AuthErrorKind.Cancelled => "Sign-in was cancelled.",
            AuthErrorKind.Network => "Couldn't reach Microsoft. Check your connection and try again.",
            _ => $"Sign-in failed: {detail}",
        };
}
