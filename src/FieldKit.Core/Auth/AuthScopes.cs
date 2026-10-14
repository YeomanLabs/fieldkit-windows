namespace FieldKit.Core.Auth;

public static class AuthScopes
{
    /// <summary>
    /// Sign-in asks for whatever delegated Graph permissions the app registration has been
    /// granted (<c>.default</c>), so adding a permission in Entra never breaks sign-in; a feature
    /// whose permission is missing gets a 403 and says which permission it needs.
    /// <see cref="GraphPermission.All"/> is the list the registration should have.
    /// </summary>
    public static readonly IReadOnlyList<string> Graph = ["https://graph.microsoft.com/.default"];
}
