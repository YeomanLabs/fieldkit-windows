namespace FieldKit.Core.Graph;

/// <summary>The cases of <see cref="GraphException"/>.</summary>
public enum GraphErrorKind
{
    /// <summary>Token refresh failed or Graph rejected the token twice; the user must sign in again.</summary>
    Unauthorized,
    Forbidden,
    NotFound,
    Throttled,
    Network,
    Server,
    /// <summary>Carries where in the response decoding failed (field names only, never values).</summary>
    Decoding,
}
