using System.Text.RegularExpressions;

namespace FieldKit.Core.Auth;

/// <summary>
/// App registration IDs. These identify the tenant and app; they are not secrets.
/// FieldKit is a public client and never holds a client secret.
/// </summary>
public sealed partial record AuthConfig(string ClientId, string TenantId)
{
    public Uri AuthorityUrl => new($"https://login.microsoftonline.com/{TenantId}");

    /// <summary>Trims and checks pasted values. The tenant may be a GUID or a verified domain.</summary>
    public static (AuthConfig? Config, AuthConfigProblem? Problem) Validated(string clientId, string tenantId)
    {
        var tenant = tenantId.Trim().ToLowerInvariant();
        if (NormalizedClientId(clientId) is not { } client) return (null, AuthConfigProblem.ClientIdInvalid);
        if (tenant is "common" or "organizations" or "consumers") return (null, AuthConfigProblem.MultiTenantAuthority);
        if (!IsNonZeroGuid(tenant) && !DomainPattern().IsMatch(tenant)) return (null, AuthConfigProblem.TenantIdInvalid);
        return (new AuthConfig(client, tenant), null);
    }

    /// <summary>The client ID trimmed and lowercased, or null while it isn't a valid one (e.g. half typed).</summary>
    public static string? NormalizedClientId(string clientId)
    {
        var client = clientId.Trim().ToLowerInvariant();
        return IsNonZeroGuid(client) ? client : null;
    }

    private static bool IsNonZeroGuid(string value) =>
        Guid.TryParseExact(value, "D", out _) && value.Any(character => character is not ('0' or '-'));

    [GeneratedRegex(@"\A[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+\z")]
    private static partial Regex DomainPattern();
}
